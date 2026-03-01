namespace Agent.Shared.Config;

public class AgentConfig
{
    // Logging
    public bool LogIdleEvents { get; set; } = true;
    public bool LogAppEvents { get; set; } = true;
    public bool LogWebEvents { get; set; } = true;

    // Local API
    // Must be injected at runtime (AGENT_LOCAL_API_TOKEN / LocalApi:Token).
    public string GlobalLocalApiToken { get; set; } = string.Empty;
    public int LocalApiPort { get; set; } = 43121;

    // Collector Intervals
    public int CollectorPollSeconds { get; set; } = 1;
    public int ActivitySampleIntervalSeconds { get; set; } = 2;
    public int HeartbeatIntervalSeconds { get; set; } = 60;
    public bool EnableLocalCollectors { get; set; } = true;
    public double IdleThresholdSeconds { get; set; } = 60;
    public int IdleSessionCheckpointSeconds { get; set; } = 0;
    public int WebSessionCheckpointSeconds { get; set; } = 0;
    public int AppSessionCheckpointSeconds { get; set; } = 0;
    public int DeviceSessionCheckpointSeconds { get; set; } = 60;
    public bool EnableScreenshots { get; set; } = false;
    public int ScreenshotIntervalMinutes { get; set; } = 5;
    public int ScreenshotRetentionDays { get; set; } = 14;

    // Outbox & Sender
    public int OutboxLeaseSeconds { get; set; } = 60;
    public int OutboxMaxBackoffSeconds { get; set; } = 300;
    public bool ClearOutboxOnStartup { get; set; } = false;
    public int SenderIntervalSeconds { get; set; } = 5;
    public int WebEventSendMinSeconds { get; set; } = 10;
    public int WebEventSendMaxSeconds { get; set; } = 30;
    public int WebSessionSendMinSeconds { get; set; } = 10;
    public int WebSessionSendMaxSeconds { get; set; } = 30;
    public int AppSessionSendMinSeconds { get; set; } = 10;
    public int AppSessionSendMaxSeconds { get; set; } = 30;
    public int IdleSessionSendMinSeconds { get; set; } = 10;
    public int IdleSessionSendMaxSeconds { get; set; } = 30;
    public int DeviceSessionSendMinSeconds { get; set; } = 10;
    public int DeviceSessionSendMaxSeconds { get; set; } = 30;
    public int MonitorSessionSendMinSeconds { get; set; } = 10;
    public int MonitorSessionSendMaxSeconds { get; set; } = 30;
    public int ScreenshotSendMinSeconds { get; set; } = 30;
    public int ScreenshotSendMaxSeconds { get; set; } = 120;

    // Ingest Endpoints
    public string CompanyEnrollmentKey { get; set; } = string.Empty;
    public string WebEventIngestEndpoint { get; set; } = "https://localhost:5002/events/web/batch";
    public string WebSessionIngestEndpoint { get; set; } = "https://localhost:5002/ingest/web-sessions";
    public string AppSessionIngestEndpoint { get; set; } = "https://localhost:5002/ingest/app-sessions";
    public string IdleSessionIngestEndpoint { get; set; } = "https://localhost:5002/ingest/idle-sessions";
    public string DeviceSessionIngestEndpoint { get; set; } = "https://localhost:5002/ingest/device-sessions";
    public string MonitorSessionIngestEndpoint { get; set; } = "https://localhost:5002/ingest/monitor-sessions";
    public string ScreenshotIngestEndpoint { get; set; } = "https://localhost:5002/ingest/screenshots";
    public string DeviceHeartbeatEndpoint { get; set; } = "https://localhost:5002/devices/heartbeat";

    // Batch Sizes
    public int WebEventBatchSize { get; set; } = 50;
    public int WebSessionBatchSize { get; set; } = 50;
    public int AppSessionBatchSize { get; set; } = 50;
    public int IdleSessionBatchSize { get; set; } = 50;
    public int DeviceSessionBatchSize { get; set; } = 50;
    public int MonitorSessionBatchSize { get; set; } = 50;
    public int ScreenshotBatchSize { get; set; } = 20;
}
