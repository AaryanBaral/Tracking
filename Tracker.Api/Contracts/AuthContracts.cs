namespace Tracker.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAtUtc,
    string CompanyId,
    string Email,
    string Role);

public sealed record ProfileResponse(
    string UserId,
    string Email,
    string Role,
    string CompanyId,
    string? CompanyName,
    bool? CompanyIsActive);
