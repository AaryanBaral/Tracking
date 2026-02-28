namespace Tracker.Api.Models;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EnrollmentKey { get; set; }
    public string EnrollmentKeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
