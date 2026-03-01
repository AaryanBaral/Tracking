namespace Agent.Shared.Models;

public record WebSessionDto(
    Guid SessionId,
    string Domain,
    string? Title,
    string? Url,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt
);

public record AppSessionDto(
    Guid SessionId,
    string ProcessName,
    string? WindowTitle,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt
);

public record IdleSessionDto(
    Guid SessionId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt
);

public record DeviceSessionDto(
    Guid SessionId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt
);

public record WebEventIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    Guid EventId,
    string Domain,
    string? Title,
    string? Url,
    DateTimeOffset Timestamp,
    string? Browser,
    bool? PipActive = null,
    bool? VideoPlaying = null,
    string? VideoUrl = null,
    string? VideoDomain = null,
    int? TabId = null
);

public record WebEventBatchItem(
    Guid EventId,
    string Domain,
    string? Title,
    string? Url,
    DateTimeOffset Timestamp,
    string? Browser,
    bool? PipActive = null,
    bool? VideoPlaying = null,
    string? VideoUrl = null,
    string? VideoDomain = null,
    int? TabId = null
);

public record WebEventBatchRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<WebEventBatchItem> Events
);

public record WebSessionIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<WebSessionDto> Sessions
);

public record AppSessionIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<AppSessionDto> Sessions
);

public record IdleSessionIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<IdleSessionDto> Sessions
);

public record DeviceSessionIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<DeviceSessionDto> Sessions
);

public record MonitorSessionDto(
    Guid SessionId,
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

public record ScreenshotDto(
    Guid ScreenshotId,
    DateTimeOffset Timestamp,
    string MonitorId,
    string FilePath,
    string TriggerReason);

public record MonitorSessionIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<MonitorSessionDto> Sessions);

public record ScreenshotIngestRequest(
    string DeviceId,
    string AgentVersion,
    Guid BatchId,
    long Sequence,
    DateTimeOffset SentAt,
    List<ScreenshotDto> Screenshots);
