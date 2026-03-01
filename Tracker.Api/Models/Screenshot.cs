namespace Tracker.Api.Models;

public class Screenshot
{
    public Guid Id { get; set; }
    public Guid ScreenshotId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string MonitorId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string TriggerReason { get; set; } = string.Empty;
}
