CREATE TABLE IF NOT EXISTS alarm_events
(
    event_id               BIGSERIAL PRIMARY KEY,

    machine_id             TEXT        NOT NULL,
    alarm_level            TEXT        NOT NULL,

    started_at             TIMESTAMPTZ NOT NULL,
    ended_at               TIMESTAMPTZ,

    message                TEXT        NOT NULL DEFAULT '',

    start_bearing_temp     DOUBLE PRECISION NOT NULL,
    start_vibration        DOUBLE PRECISION NOT NULL,
    start_tire_wear        DOUBLE PRECISION NOT NULL,

    peak_bearing_temp      DOUBLE PRECISION NOT NULL,
    peak_vibration         DOUBLE PRECISION NOT NULL,
    peak_tire_wear         DOUBLE PRECISION NOT NULL,

    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_alarm_event_level
        CHECK (alarm_level IN ('WARNING', 'ALARM')),

    CONSTRAINT chk_alarm_event_time
        CHECK (
            ended_at IS NULL
            OR ended_at >= started_at
        )
);


-- 설비별 알람 발생 이력 조회용
CREATE INDEX IF NOT EXISTS idx_alarm_events_machine_time
ON alarm_events
(
    machine_id,
    started_at DESC
);


-- 한 설비에는 활성 알람을 한 건만 허용한다.
CREATE UNIQUE INDEX IF NOT EXISTS idx_alarm_events_one_active
ON alarm_events (machine_id)
WHERE ended_at IS NULL;