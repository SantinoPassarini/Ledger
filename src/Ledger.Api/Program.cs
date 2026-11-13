using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ledger.Api.Auth;
using Ledger.Api.Configuration;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Ledger.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Ledger.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            if (builder.Environment.IsProduction())
            {
                ProductionConfigurationValidator.Validate(builder.Configuration);
            }

            builder.Services
                .AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste the access token returned by POST /api/v1/auth/login."
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.Configure<LedgerOptions>(
                builder.Configuration.GetSection(LedgerOptions.SectionName));
            builder.Services
                .AddOptions<JwtOptions>()
                .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Issuer),
                    "Jwt:Issuer is required.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Audience),
                    "Jwt:Audience is required.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.SigningKey) &&
                        Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
                    "Jwt:SigningKey is required and must contain at least 32 characters.")
                .Validate(
                    options => options.AccessTokenMinutes > 0,
                    "Jwt:AccessTokenMinutes must be greater than zero.")
                .ValidateOnStart();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // Render terminates TLS and forwards requests from its proxy. The container
                // port is not reachable from the internet, so the proxy headers can be trusted.
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            // Resolve the validation parameters from IOptions so the same configuration used by
            // JwtTokenService is used here, including configuration added by test hosts.
            builder.Services
                .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
                {
                    var jwt = jwtOptions.Value;

                    if (string.IsNullOrWhiteSpace(jwt.SigningKey) ||
                        Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
                    {
                        throw new InvalidOperationException(
                            "Jwt:SigningKey is required and must contain at least 32 characters.");
                    }

                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "name"
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("login", limiter =>
                {
                    limiter.PermitLimit = 20;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.AutoReplenishment = true;
                });
                options.AddFixedWindowLimiter("register", limiter =>
                {
                    limiter.PermitLimit = 20;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.AutoReplenishment = true;
                });
            });

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
            builder.Services.AddScoped<JwtTokenService>();
            builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
            builder.Services.AddHostedService<UserProvisioningService>();

            builder.Services.AddDbContext<LedgerDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Ledger")));

            var app = builder.Build();

            // Must run before HTTPS redirection, rate limiting and authentication so
            // Request.Scheme and RemoteIpAddress reflect the original client.
            app.UseForwardedHeaders();

            var registration = app.Services
                .GetRequiredService<IOptions<LedgerOptions>>()
                .Value
                .Registration;

            if (registration.Enabled && string.IsNullOrWhiteSpace(registration.InviteCode))
            {
                app.Logger.LogError(
                    "Self-registration is enabled but Ledger:Registration:InviteCode is not set. " +
                    "Registration requests will be rejected until the invite code is configured.");
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (!app.Environment.IsEnvironment("Testing"))
            {
                app.UseHttpsRedirection();
            }

            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/health", async (LedgerDbContext db, ILogger<Program> logger) =>
                {
                    try
                    {
                        if (await db.Database.CanConnectAsync())
                        {
                            return Results.Ok(new { status = "Healthy", database = "Reachable" });
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Database health check failed.");
                    }

                    return Results.Json(
                        new { status = "Unhealthy", database = "Unreachable" },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                })
                .AllowAnonymous();

            app.MapControllers();

            app.Run();
        }
    }
}
