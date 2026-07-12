-- TimescaleDB 확장 기능을 현재 PostgreSQL DB에 활성화한다.
CREATE EXTENSION IF NOT EXISTS timescaledb;


-- 미분기 센서 원본 데이터를 저장하는 테이블이다.
CREATE TABLE IF NOT EXISTS sensor_readings
(
    -- 실제 측정이 이루어진 시간이다.
    time            TIMESTAMPTZ      NOT NULL,

    -- 설비 식별자이다. 이후 PV-002, PV-003으로 확장할 수 있다.
    machine_id      TEXT             NOT NULL,

    is_running      BOOLEAN          NOT NULL,

    bowl_rpm        DOUBLE PRECISION NOT NULL,
    motor_current   DOUBLE PRECISION NOT NULL,
    bearing_temp    DOUBLE PRECISION NOT NULL,
    vibration       DOUBLE PRECISION NOT NULL,
    tire_wear       DOUBLE PRECISION NOT NULL,

    alarm_level     TEXT             NOT NULL,
    alarm_message   TEXT             NOT NULL DEFAULT '',

    -- 서버가 데이터를 실제로 DB에 저장한 시간이다.
    received_at     TIMESTAMPTZ      NOT NULL DEFAULT NOW(),

    -- TimescaleDB Hypertable의 고유키에는
    -- 시간 파티션 열인 time이 반드시 포함되어야 한다.
    PRIMARY KEY (machine_id, time),

    CONSTRAINT chk_sensor_alarm_level
        CHECK (alarm_level IN ('NORMAL', 'WARNING', 'ALARM')),

    CONSTRAINT chk_tire_wear_range
        CHECK (tire_wear >= 0 AND tire_wear <= 100)
);


-- 일반 PostgreSQL 테이블을 TimescaleDB Hypertable로 변환한다.
-- 센서값이 0.5초마다 저장되므로 개발 단계에서는 1일 Chunk를 사용한다.
SELECT create_hypertable(
    'sensor_readings',
    by_range('time', INTERVAL '1 day'),
    if_not_exists => TRUE
);