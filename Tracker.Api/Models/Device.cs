namespace Tracker.Api.Models;

public class Device
{
    public string Id { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public string? Hostname { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
}
