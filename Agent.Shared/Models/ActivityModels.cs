namespace Agent.Shared.Models;

public sealed record WindowBoundsDto(
    int X,
    int Y,
    int Width,
    int Height);

public sealed record MonitorInfoDto(
    string MonitorId,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary);

public sealed record VisibleWindowDto(
    string ProcessName,
    string? WindowTitle,
    WindowBoundsDto WindowBounds,
    int ZOrder,
    bool IsMinimized,
    bool IsVisible,
    string MonitorId);

public sealed record ActivitySample(
    string ProcessName,
    string? WindowTitle,
    string MonitorId,
    WindowBoundsDto WindowBounds,
    bool IsSplitScreen,
    bool IsPiPActive,
    bool IsVideoPlaying,
    int MonitorCount,
    int AttentionScore,
    DateTimeOffset TimestampUtc);

public sealed record PipStateEvent(
    bool PipActive,
    bool IsVideoPlaying,
    string? VideoUrl,
    string? VideoDomain,
    int? TabId,
    DateTimeOffset TimestampUtc);

public sealed record ScreenshotCaptureRequest(
    string TriggerReason,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<MonitorInfoDto> Monitors);

public sealed record ScreenshotCaptureResult(
    string MonitorId,
    string FilePath,
    string TriggerReason,
    DateTimeOffset CapturedAtUtc);
