namespace Tracker.Api.Contracts;

public sealed record CompanyListItem(
    Guid Id,
    string Name,
    bool IsActive,
    int DeviceCount,
    string? EnrollmentKey);

public sealed record CreateCompanyRequest(
    string Name,
    string EnrollmentKey,
    string AdminEmail,
    string AdminPassword,
    bool IsActive = true);

public sealed record CreateCompanyResponse(
    Guid Id,
    string Name,
    bool IsActive,
    string EnrollmentKey);

public sealed record UpdateCompanyRequest(
    string Name,
    string EnrollmentKey,
    bool IsActive);
