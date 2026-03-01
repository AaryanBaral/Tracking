namespace Tracker.Api.Services.Models;

public sealed record WebEventRow(
    Guid EventId,
    string DeviceId,
    string Domain,
    string? Title,
    string? Url,
    DateTimeOffset Timestamp,
    string? Browser,
    bool? PipActive,
    bool? VideoPlaying,
    string? VideoUrl,
    string? VideoDomain,
    int? TabId,
    DateTimeOffset ReceivedAt);

public sealed record WebSessionRow(
    Guid SessionId,
    string DeviceId,
    string Domain,
    string? Title,
    string? Url,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record AppSessionRow(
    Guid SessionId,
    string DeviceId,
    string ProcessName,
    string? WindowTitle,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record IdleSessionRow(
    Guid SessionId,
    string DeviceId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record DeviceSessionRow(
    Guid SessionId,
    string DeviceId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public sealed record MonitorSessionRow(
    Guid SessionId,
    string DeviceId,
    DateTimeOffset Timestamp,
    string MonitorId,
    int ResolutionWidth,
    int ResolutionHeight,
    string ActiveWindowProcess,
    string? ActiveWindowTitle,
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    bool IsSplitScreen,
    bool IsPiPActive,
    int AttentionScore);

public sealed record ScreenshotRow(
    Guid ScreenshotId,
    string DeviceId,
    DateTimeOffset Timestamp,
    string MonitorId,
    string FilePath,
    string TriggerReason);
