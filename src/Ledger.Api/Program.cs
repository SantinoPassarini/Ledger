using System.Text.Json.Serialization;
using Ledger.Api.Data;
using Ledger.Api.Options;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services
                .AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.Configure<LedgerOptions>(
                builder.Configuration.GetSection(LedgerOptions.SectionName));
            builder.Services.AddDbContext<LedgerDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Ledger")));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (!app.Environment.IsEnvironment("Testing"))
            {
                app.UseHttpsRedirection();
            }

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
            });

            app.MapControllers();

            app.Run();
        }
    }
}
