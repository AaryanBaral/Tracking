using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tracker.Api.Contracts;
using Tracker.Api.Data;
using Tracker.Api.Security;

namespace Tracker.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", LoginAsync)
            .RequireRateLimiting("auth");
        app.MapGet("/auth/me", GetProfileAsync);
        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        TrackerDbContext db,
        IOptions<JwtOptions> options,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest("email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest("password is required.");
        }

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        if (user is null)
        {
            logger.LogWarning("Login failed.");
            return Results.Unauthorized();
        }

        if (!string.Equals(user.Role, ClaimsExtensions.RoleSystemAdmin, StringComparison.OrdinalIgnoreCase))
        {
            var company = await db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == user.CompanyId && c.IsActive, ct);
            if (company is null)
            {
                logger.LogWarning("Login failed.");
                return Results.Unauthorized();
            }
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed.");
            return Results.Unauthorized();
        }

        var jwtOptions = options.Value;
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(jwtOptions.TokenMinutes <= 0 ? 720 : jwtOptions.TokenMinutes);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimsExtensions.CompanyIdClaim, user.CompanyId.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            },
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return Results.Ok(new LoginResponse(
            serialized,
            expires,
            user.CompanyId.ToString(),
            user.Email,
            user.Role));
    }

    private static async Task<IResult> GetProfileAsync(
        HttpRequest request,
        TrackerDbContext db,
        IOptions<JwtOptions> options,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!TryValidateToken(request, options.Value, out var user, out var error))
        {
            logger.LogWarning("Profile auth failed: {error}", error);
            return Results.Unauthorized();
        }

        var rawUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? user.FindFirstValue(ClaimTypes.Email);

        Models.User? dbUser = null;
        if (Guid.TryParse(rawUserId, out var userId))
        {
            dbUser = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        }

        if (dbUser is null && !string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            dbUser = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive, ct);
        }
        if (dbUser is null)
        {
            return Results.Unauthorized();
        }

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dbUser.CompanyId, ct);

        if (!string.Equals(dbUser.Role, ClaimsExtensions.RoleSystemAdmin, StringComparison.OrdinalIgnoreCase))
        {
            if (company is null || !company.IsActive)
            {
                return Results.Unauthorized();
            }
        }

        return Results.Ok(new ProfileResponse(
            dbUser.Id.ToString(),
            dbUser.Email,
            dbUser.Role,
            dbUser.CompanyId.ToString(),
            company?.Name,
            company?.IsActive));
    }

    private static bool TryValidateToken(
        HttpRequest request,
        JwtOptions options,
        out ClaimsPrincipal principal,
        out string? error)
    {
        principal = new ClaimsPrincipal();
        error = null;

        if (!request.Headers.TryGetValue("Authorization", out var authHeader) || authHeader.Count == 0)
        {
            error = "missing Authorization header";
            return false;
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            error = "invalid auth scheme";
            return false;
        }

        var token = headerValue.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            error = "missing bearer token";
            return false;
        }

        var keyBytes = System.Text.Encoding.UTF8.GetBytes(options.SigningKey ?? string.Empty);
        if (keyBytes.Length < 32)
        {
            error = "signing key too short";
            return false;
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            principal = handler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            return false;
        }
    }
}
