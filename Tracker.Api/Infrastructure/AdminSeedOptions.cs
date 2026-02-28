namespace Tracker.Api.Infrastructure;

public sealed class AdminSeedOptions
{
    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "SystemAdmin";
    public string CompanyName { get; set; } = "System";
    public string? CompanyEnrollmentKey { get; set; }
}
