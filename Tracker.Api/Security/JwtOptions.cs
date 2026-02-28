namespace Tracker.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "EmployeeTracker";
    public string Audience { get; set; } = "EmployeeTrackerAdmin";
    public string SigningKey { get; set; } = "change-this-to-a-random-string";
    public int TokenMinutes { get; set; } = 720;
}
