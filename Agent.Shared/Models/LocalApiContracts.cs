using System.Text.Json.Serialization;

namespace Agent.Shared.Models;

public sealed record AppFocusRequest(
    [property: JsonPropertyName("appName")] string AppName,
    [property: JsonPropertyName("windowTitle")] string? WindowTitle,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset TimestampUtc);

public sealed record IdleSampleRequest(
    [property: JsonPropertyName("idleSeconds")] double IdleSeconds,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset TimestampUtc);

public sealed record ActivitySampleRequest(
    [property: JsonPropertyName("processName")] string ProcessName,
    [property: JsonPropertyName("windowTitle")] string? WindowTitle,
    [property: JsonPropertyName("monitorId")] string MonitorId,
    [property: JsonPropertyName("monitorWidth")] int MonitorWidth,
    [property: JsonPropertyName("monitorHeight")] int MonitorHeight,
    [property: JsonPropertyName("windowBounds")] WindowBoundsDto WindowBounds,
    [property: JsonPropertyName("isSplitScreen")] bool IsSplitScreen,
    [property: JsonPropertyName("isPiPActive")] bool IsPiPActive,
    [property: JsonPropertyName("isVideoPlaying")] bool IsVideoPlaying,
    [property: JsonPropertyName("monitorCount")] int MonitorCount,
    [property: JsonPropertyName("attentionScore")] int AttentionScore,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset TimestampUtc);

public sealed record ScreenshotEventRequest(
    [property: JsonPropertyName("monitorId")] string MonitorId,
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("triggerReason")] string TriggerReason,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset TimestampUtc);

public sealed record LocalHealthResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("lastFlushAtUtc")] DateTimeOffset? LastFlushAtUtc = null,
    [property: JsonPropertyName("deviceId")] string? DeviceId = null,
    [property: JsonPropertyName("agentVersion")] string? AgentVersion = null);

public sealed record LocalVersionResponse(
    [property: JsonPropertyName("contract")] string Contract,
    [property: JsonPropertyName("deviceId")] string? DeviceId,
    [property: JsonPropertyName("agentVersion")] string? AgentVersion,
    [property: JsonPropertyName("port")] int Port);

public sealed record LocalDiagResponse(
    [property: JsonPropertyName("contract")] string Contract,
    [property: JsonPropertyName("deviceId")] string? DeviceId,
    [property: JsonPropertyName("agentVersion")] string? AgentVersion,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("serverTimeUtc")] DateTimeOffset ServerTimeUtc,
    [property: JsonPropertyName("outbox")] LocalDiagOutbox Outbox);

public sealed record LocalDiagOutbox(
    [property: JsonPropertyName("pendingByType")] Dictionary<string, int>? PendingByType,
    [property: JsonPropertyName("lastFlushAtUtc")] DateTimeOffset? LastFlushAtUtc,
    [property: JsonPropertyName("available")] bool Available);

public sealed record LocalPipStateResponse(
    [property: JsonPropertyName("pipActive")] bool PipActive,
    [property: JsonPropertyName("videoPlaying")] bool VideoPlaying,
    [property: JsonPropertyName("videoUrl")] string? VideoUrl,
    [property: JsonPropertyName("videoDomain")] string? VideoDomain,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset TimestampUtc);
