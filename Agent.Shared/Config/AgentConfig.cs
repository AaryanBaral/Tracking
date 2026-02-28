namespace Agent.Shared.Config;

public class AgentConfig
{
    // Logging
    public bool LogIdleEvents { get; set; } = true;
    public bool LogAppEvents { get; set; } = true;
    public bool LogWebEvents { get; set; } = true;

    // Local API
    public string GlobalLocalApiToken { get; set; } = "dev-token-123";
    public int LocalApiPort { get; set; } = 43121;

    // Collector Intervals
    public int CollectorPollSeconds { get; set; } = 1;
    public int HeartbeatIntervalSeconds { get; set; } = 60;
    public bool EnableLocalCollectors { get; set; } = true;
    public double IdleThresholdSeconds { get; set; } = 60;
    public int IdleSessionCheckpointSeconds { get; set; } = 0;
    public int WebSessionCheckpointSeconds { get; set; } = 0;
    public int AppSessionCheckpointSeconds { get; set; } = 0;
    public int DeviceSessionCheckpointSeconds { get; set; } = 60;

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

    // Ingest Endpoints
    public string CompanyEnrollmentKey { get; set; } = string.Empty;
    public string WebEventIngestEndpoint { get; set; } = "http://localhost:5000/events/web/batch";
    public string WebSessionIngestEndpoint { get; set; } = "http://localhost:5000/ingest/web-sessions";
    public string AppSessionIngestEndpoint { get; set; } = "http://localhost:5000/ingest/app-sessions";
    public string IdleSessionIngestEndpoint { get; set; } = "http://localhost:5000/ingest/idle-sessions";
    public string DeviceSessionIngestEndpoint { get; set; } = "http://localhost:5000/ingest/device-sessions";
    public string DeviceHeartbeatEndpoint { get; set; } = "http://localhost:5000/devices/heartbeat";

    // Batch Sizes
    public int WebEventBatchSize { get; set; } = 50;
    public int WebSessionBatchSize { get; set; } = 50;
    public int AppSessionBatchSize { get; set; } = 50;
    public int IdleSessionBatchSize { get; set; } = 50;
    public int DeviceSessionBatchSize { get; set; } = 50;
}
