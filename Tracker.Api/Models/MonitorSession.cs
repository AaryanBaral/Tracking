namespace Tracker.Api.Models;

public class MonitorSession
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string MonitorId { get; set; } = string.Empty;
    public int ResolutionWidth { get; set; }
    public int ResolutionHeight { get; set; }
    public string ActiveWindowProcess { get; set; } = string.Empty;
    public string? ActiveWindowTitle { get; set; }
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public bool IsSplitScreen { get; set; }
    public bool IsPiPActive { get; set; }
    public int AttentionScore { get; set; }
}
