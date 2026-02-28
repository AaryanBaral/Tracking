# Production Logging Deployment Guide

This folder provides production-oriented logging deployment assets for Employee Tracker.

## What is included

- `docker-compose.logging.yml`:
  - Runs `tracker-api` with JSON logs to stdout
  - Central sink stack: `loki`, `promtail`, `alertmanager`
  - Fallback local rotation for app container logs (`json-file`, 20MB x 10 files)
- `loki/loki-config.yaml`:
  - Retention enabled (`168h` = 7 days)
  - WAL enabled for durability
  - Ruler enabled for alert rule evaluation
- `promtail/promtail-config.yaml`:
  - Ships Docker container logs to Loki
- `rules/production-alerts.yaml`:
  - Crash loop, 5xx spikes, heartbeat failures, outbox send failures
- `alertmanager/alertmanager.yml`:
  - Alert route/receiver template

## Centralized logging sink

Default sink: Loki (`http://loki:3100`) with Promtail ingestion.

General policy:
- App logs stay structured JSON (Console formatter).
- Promtail forwards logs to centralized sink.
- Keep payload-redaction in app logs (already handled in code).

## Rotation and retention

### Local fallback rotation (container runtime)
Configured in `docker-compose.logging.yml`:
- Driver: `json-file`
- `max-size: 20m`
- `max-file: 10`

This caps per-container local logs to ~200MB.

### Central retention
Configured in `loki/loki-config.yaml`:
- `retention_period: 168h` (7 days)
- Adjust to 720h (30 days) if compliance requires.

## Alerting

Rules are in `rules/production-alerts.yaml` and evaluated by Loki ruler.

Current alerts:
- `TrackerApiCrashLoop` (critical)
- `TrackerApiHigh5xx` (critical)
- `AgentHeartbeatFailures` (warning)
- `AgentOutboxSendFailures` (warning)

Update `alertmanager/alertmanager.yml` receiver to real integration:
- Slack
- PagerDuty
- Email/webhook

Required runtime env:
- `ALERT_WEBHOOK_URL` (see `.env.example`)

## Secure audit channel

General production logs should not include sensitive details.

Recommended setup:
- Keep current redacted general logs in default sink.
- Route authentication/audit category logs to a restricted sink/index/tenant.
  - Example label strategy: `channel=audit`, `access=restricted`.
  - Restrict read access to security/audit roles only.

## Runtime configuration enforcement

Use environment-based configuration in deployment (not code constants):
- `ASPNETCORE_ENVIRONMENT=Production`
- `Logging__Console__FormatterName=json`
- `Logging__LogLevel__Default=Warning`

Optional hardening:
- Inject via secrets/config manager at deploy time.
- Block startup in CI/CD if required logging env vars are missing.

## Verification playbook

Run stack:

```bash
cp deploy/logging/.env.example deploy/logging/.env
# update ALERT_WEBHOOK_URL in deploy/logging/.env
docker compose --env-file deploy/logging/.env -f deploy/logging/docker-compose.logging.yml up -d --build
```

### 1) Central sink ingestion verified
- Generate traffic/errors against API.
- Query Loki for recent logs (`job="docker"`).

### 2) Rotation + retention verified
- Force log volume burst.
- Confirm Docker `json-file` rotates at configured size/count.
- Confirm Loki retention deletes logs older than retention window.

### 3) Alerts verified
- Simulate repeated 5xx and confirm `TrackerApiHigh5xx` fires.
- Simulate crash to confirm `TrackerApiCrashLoop` fires.

### 4) Sensitive data redaction verified
- Review logs for absence of:
  - enrollment keys
  - passwords
  - JWTs
  - raw request/response bodies
  - URL/title/windowTitle payload content

### 5) Agent failure-path verified
- Simulate backend outage.
- Confirm `Heartbeat failed` / `Failed to send batch` are visible in sink.
- Confirm warning alerts fire at expected thresholds.

## Notes

- This repo currently ships API container config. If Agent runs as host service, send host logs to the same central sink (journald forwarder/Promtail/Vector/Fluent Bit).
- Keep alert thresholds tuned with real baseline traffic; start conservative and refine.
