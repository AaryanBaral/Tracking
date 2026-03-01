-- 2026-03-01: Multi-monitor, split-screen, PiP, screenshot persistence

ALTER TABLE web_events
    ADD COLUMN IF NOT EXISTS pip_active BOOLEAN NULL,
    ADD COLUMN IF NOT EXISTS video_playing BOOLEAN NULL,
    ADD COLUMN IF NOT EXISTS video_url TEXT NULL,
    ADD COLUMN IF NOT EXISTS video_domain TEXT NULL,
    ADD COLUMN IF NOT EXISTS tab_id INTEGER NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_web_events_video_url_len'
    ) THEN
        ALTER TABLE web_events
            ADD CONSTRAINT chk_web_events_video_url_len
            CHECK (video_url IS NULL OR char_length(video_url) <= 2048);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_web_events_video_domain_len'
    ) THEN
        ALTER TABLE web_events
            ADD CONSTRAINT chk_web_events_video_domain_len
            CHECK (video_domain IS NULL OR char_length(video_domain) <= 255);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS monitor_sessions (
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
    CONSTRAINT chk_monitor_sessions_resolution CHECK (resolution_width > 0 AND resolution_height > 0),
    CONSTRAINT chk_monitor_sessions_window_size CHECK (window_width >= 0 AND window_height >= 0),
    CONSTRAINT chk_monitor_sessions_attention CHECK (attention_score >= 0 AND attention_score <= 100),
    CONSTRAINT chk_monitor_sessions_monitor_id_len CHECK (char_length(monitor_id) <= 128),
    CONSTRAINT chk_monitor_sessions_process_len CHECK (char_length(active_window_process) <= 255),
    CONSTRAINT chk_monitor_sessions_title_len CHECK (active_window_title IS NULL OR char_length(active_window_title) <= 512)
);

CREATE INDEX IF NOT EXISTS ix_monitor_sessions_device_timestamp
    ON monitor_sessions(device_id, timestamp);

CREATE INDEX IF NOT EXISTS ix_monitor_sessions_device_monitor_timestamp
    ON monitor_sessions(device_id, monitor_id, timestamp);

CREATE TABLE IF NOT EXISTS screenshots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    screenshot_id UUID NOT NULL UNIQUE,
    device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    timestamp TIMESTAMPTZ NOT NULL,
    monitor_id TEXT NOT NULL,
    file_path TEXT NOT NULL,
    trigger_reason TEXT NOT NULL,
    CONSTRAINT chk_screenshots_monitor_id_len CHECK (char_length(monitor_id) <= 128),
    CONSTRAINT chk_screenshots_file_path_len CHECK (char_length(file_path) <= 2048),
    CONSTRAINT chk_screenshots_trigger_reason_len CHECK (char_length(trigger_reason) <= 128)
);

CREATE INDEX IF NOT EXISTS ix_screenshots_device_timestamp
    ON screenshots(device_id, timestamp);
