CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE companies (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    enrollment_key VARCHAR(256) NULL,
    enrollment_key_hash VARCHAR(128) NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE devices (
    id TEXT PRIMARY KEY,
    company_id UUID NULL REFERENCES companies(id) ON DELETE CASCADE,
    hostname TEXT NULL,
    display_name TEXT NULL,
    last_seen_at TIMESTAMPTZ NOT NULL,
    last_reviewed_at TIMESTAMPTZ NULL
);

CREATE INDEX ix_devices_company_id ON devices(company_id);

CREATE TABLE users (
    id UUID PRIMARY KEY,
    company_id UUID NOT NULL REFERENCES companies(id) ON DELETE CASCADE,
    email VARCHAR(320) NOT NULL UNIQUE,
    password_hash VARCHAR(512) NOT NULL,
    role VARCHAR(64) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE ingest_cursors (
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    stream TEXT NOT NULL,
    last_sequence BIGINT NOT NULL,
    last_batch_id UUID NOT NULL,
    last_sent_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (device_id, stream)
);

CREATE TABLE web_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    domain TEXT NOT NULL,
    title TEXT NULL,
    url TEXT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    browser TEXT NULL,
    pip_active BOOLEAN NULL,
    video_playing BOOLEAN NULL,
    video_url TEXT NULL,
    video_domain TEXT NULL,
    tab_id INTEGER NULL,
    received_at TIMESTAMPTZ NOT NULL,
    CHECK (char_length(device_id) <= 64),
    CHECK (char_length(domain) <= 255),
    CHECK (title IS NULL OR char_length(title) <= 512),
    CHECK (url IS NULL OR char_length(url) <= 2048),
    CHECK (browser IS NULL OR char_length(browser) <= 64),
    CHECK (video_url IS NULL OR char_length(video_url) <= 2048),
    CHECK (video_domain IS NULL OR char_length(video_domain) <= 255)
);

CREATE INDEX ix_web_events_device_timestamp ON web_events(device_id, timestamp);

CREATE TABLE web_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    domain TEXT NOT NULL,
    title TEXT NULL,
    url TEXT NULL,
    start_at TIMESTAMPTZ NOT NULL,
    end_at TIMESTAMPTZ NOT NULL,
    CHECK (end_at > start_at),
    CHECK (char_length(domain) <= 255),
    CHECK (title IS NULL OR char_length(title) <= 512),
    CHECK (url IS NULL OR char_length(url) <= 2048)
);

CREATE INDEX ix_web_device_start ON web_sessions(device_id, start_at);
CREATE INDEX ix_web_device_domain_start ON web_sessions(device_id, domain, start_at);

CREATE TABLE app_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    process_name TEXT NOT NULL,
    window_title TEXT NULL,
    start_at TIMESTAMPTZ NOT NULL,
    end_at TIMESTAMPTZ NOT NULL,
    CHECK (end_at > start_at),
    CHECK (char_length(process_name) <= 255),
    CHECK (window_title IS NULL OR char_length(window_title) <= 512)
);

CREATE INDEX ix_app_device_start ON app_sessions(device_id, start_at);
CREATE INDEX ix_app_device_process_start ON app_sessions(device_id, process_name, start_at);

CREATE TABLE idle_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    start_at TIMESTAMPTZ NOT NULL,
    end_at TIMESTAMPTZ NOT NULL,
    CHECK (end_at > start_at)
);

CREATE INDEX ix_idle_device_start ON idle_sessions(device_id, start_at);

CREATE TABLE monitor_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    timestamp TIMESTAMPTZ NOT NULL,
    monitor_id TEXT NOT NULL,
    resolution_width INTEGER NOT NULL,
    resolution_height INTEGER NOT NULL,
    active_window_process TEXT NOT NULL,
    active_window_title TEXT NULL,
    window_x INTEGER NOT NULL,
    window_y INTEGER NOT NULL,
    window_width INTEGER NOT NULL,
    window_height INTEGER NOT NULL,
    is_split_screen BOOLEAN NOT NULL DEFAULT FALSE,
    is_pip_active BOOLEAN NOT NULL DEFAULT FALSE,
    attention_score INTEGER NOT NULL DEFAULT 100,
    CHECK (resolution_width > 0),
    CHECK (resolution_height > 0),
    CHECK (window_width >= 0),
    CHECK (window_height >= 0),
    CHECK (attention_score >= 0 AND attention_score <= 100),
    CHECK (char_length(monitor_id) <= 128),
    CHECK (char_length(active_window_process) <= 255),
    CHECK (active_window_title IS NULL OR char_length(active_window_title) <= 512)
);

CREATE INDEX ix_monitor_sessions_device_timestamp ON monitor_sessions(device_id, timestamp);
CREATE INDEX ix_monitor_sessions_device_monitor_timestamp ON monitor_sessions(device_id, monitor_id, timestamp);

CREATE TABLE screenshots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    screenshot_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    timestamp TIMESTAMPTZ NOT NULL,
    monitor_id TEXT NOT NULL,
    file_path TEXT NOT NULL,
    trigger_reason TEXT NOT NULL,
    CHECK (char_length(monitor_id) <= 128),
    CHECK (char_length(file_path) <= 2048),
    CHECK (char_length(trigger_reason) <= 128)
);

CREATE INDEX ix_screenshots_device_timestamp ON screenshots(device_id, timestamp);
