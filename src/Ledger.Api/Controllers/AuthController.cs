using System.Security.Cryptography;
using System.Text;
using Ledger.Api.Auth;
using Ledger.Api.Contracts.Auth;
using Ledger.Api.Data;
using Ledger.Api.Domain;
using Ledger.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ledger.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly LedgerDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtTokenService _tokenService;
    private readonly RegistrationOptions _registration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        LedgerDbContext db,
        ICurrentUser currentUser,
        IPasswordHasher<User> passwordHasher,
        JwtTokenService tokenService,
        IOptions<LedgerOptions> ledgerOptions,
        ILogger<AuthController> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _registration = ledgerOptions.Value.Registration;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var normalizedUsername = Domain.User.NormalizeUsername(identifier);
        var normalizedEmail = Domain.User.NormalizeEmail(identifier);

        var user = await _db.Users
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.NormalizedUsername == normalizedUsername ||
                    candidate.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return InvalidCredentials();
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var token = _tokenService.CreateAccessToken(user);

        if (token is null)
        {
            return TokenServiceUnavailable();
        }

        return Ok(new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            ToResponse(user)));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<RegisterResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!_registration.Enabled)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Registration is disabled",
                detail: "This Ledger instance does not accept self-registration.");
        }

        if (string.IsNullOrWhiteSpace(_registration.InviteCode))
        {
            _logger.LogError(
                "Registration is enabled but Ledger:Registration:InviteCode is not configured. " +
                "Registration is rejected until the invite code is set.");

            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Registration is not available",
                detail: "The server is missing its registration invite code.");
        }

        if (!InviteCodeMatches(request.InviteCode, _registration.InviteCode))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid invite code",
                detail: "The invite code is not valid.");
        }

        var username = request.Username.Trim();

        if (!IsValidUsername(username))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid username",
                detail: "Use 3 to 64 letters, digits, dots, hyphens or underscores. " +
                        "Usernames cannot contain spaces or @.");
        }

        var normalizedUsername = Domain.User.NormalizeUsername(username);
        var providedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        var emailGenerated = providedEmail is null;
        var email = providedEmail ?? $"{username}@ledger.local";
        var normalizedEmail = Domain.User.NormalizeEmail(email);

        var existing = await _db.Users
            .AsNoTracking()
            .Where(candidate =>
                candidate.NormalizedUsername == normalizedUsername ||
                candidate.NormalizedEmail == normalizedEmail)
            .Select(candidate => new
            {
                candidate.NormalizedUsername,
                candidate.NormalizedEmail
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var conflict = existing.NormalizedUsername == normalizedUsername
                ? "username"
                : "email";

            return AccountAlreadyExists(conflict);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? username
            : request.DisplayName.Trim();

        if (displayName.Length > 100)
        {
            displayName = displayName[..100];
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalizedUsername,
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        UserAccountFactory.AddDefaultCategories(_db, user.Id);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // The unique indexes close the race between the duplicate check and the insert.
            return AccountAlreadyExists("username or email");
        }

        var token = _tokenService.CreateAccessToken(user);

        if (token is null)
        {
            return TokenServiceUnavailable();
        }

        return StatusCode(
            StatusCodes.Status201Created,
            new RegisterResponse(
                token.AccessToken,
                token.ExpiresAtUtc,
                ToResponse(user),
                emailGenerated));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId && candidate.IsActive,
                cancellationToken);

        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (verification == PasswordVerificationResult.Failed)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid current password",
                detail: "The current password is incorrect.");
        }

        var newMatchesCurrent = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.NewPassword);

        if (newMatchesCurrent != PasswordVerificationResult.Failed)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Password unchanged",
                detail: "The new password must be different from the current password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private ActionResult<LoginResponse> InvalidCredentials()
    {
        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials",
            detail: "Invalid username, email or password.");
    }

    private ActionResult<RegisterResponse> AccountAlreadyExists(string field)
    {
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Account already exists",
            detail: $"The {field} is already in use.");
    }

    private ObjectResult TokenServiceUnavailable()
    {
        return Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Authentication is not configured",
            detail: "Configure Jwt:SigningKey, Jwt:Issuer and Jwt:Audience on the API.");
    }

    private static bool InviteCodeMatches(string provided, string expected)
    {
        // Hash both values so FixedTimeEquals always compares the same number of bytes.
        var providedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(provided.Trim()));
        var expectedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(expected.Trim()));

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private static bool IsValidUsername(string username)
    {
        if (username.Length is < 3 or > 64)
        {
            return false;
        }

        foreach (var character in username)
        {
            if (!char.IsLetterOrDigit(character) &&
                character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }

    private static UserResponse ToResponse(User user)
    {
        return new UserResponse(user.Id, user.Username, user.Email, user.DisplayName);
    }
}
