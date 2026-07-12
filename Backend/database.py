from __future__ import annotations

import os
from datetime import datetime
from typing import Any

from dotenv import load_dotenv
from psycopg.rows import dict_row
from psycopg_pool import AsyncConnectionPool


# Backend/.env 파일의 값을 환경변수로 불러온다.
load_dotenv()

DATABASE_URL = os.getenv("DATABASE_URL", "").strip()

if not DATABASE_URL:
    raise RuntimeError(
        "DATABASE_URL이 없습니다. Backend/.env 파일을 확인하세요."
    )


# FastAPI 비동기 실행 구조에 맞춘 PostgreSQL 연결 풀이다.
# 실제 DB 연결은 FastAPI 시작 시 open_database()에서 연다.
db_pool = AsyncConnectionPool(
    conninfo=DATABASE_URL,
    min_size=1,
    max_size=5,
    open=False,
    kwargs={
        "autocommit": True,
        "row_factory": dict_row,
    },
)


async def open_database() -> None:
    """FastAPI 시작 시 TimescaleDB 연결 풀을 연다."""

    await db_pool.open()
    await db_pool.wait()

    print("[DB] TimescaleDB connection pool opened")


async def close_database() -> None:
    """FastAPI 종료 시 DB 연결 풀을 안전하게 닫는다."""

    await db_pool.close()

    print("[DB] TimescaleDB connection pool closed")


async def database_is_ready() -> bool:
    """DB 접속 및 간단한 쿼리가 가능한지 확인한다."""

    try:
        async with db_pool.connection() as connection:
            cursor = await connection.execute("SELECT 1 AS result;")
            row = await cursor.fetchone()

        return row is not None and row["result"] == 1

    except Exception as exception:
        print(f"[DB ERROR] Health check failed: {exception}")
        return False


async def save_sensor_reading(data: dict[str, Any]) -> None:
    """미분기 센서 데이터 한 건을 Hypertable에 저장한다."""

    measured_at = datetime.fromisoformat(data["timestamp"])

    query = """
        INSERT INTO sensor_readings
        (
            time,
            machine_id,
            is_running,
            bowl_rpm,
            motor_current,
            bearing_temp,
            vibration,
            tire_wear,
            alarm_level,
            alarm_message
        )
        VALUES
        (
            %s, %s, %s, %s, %s,
            %s, %s, %s, %s, %s
        )
        ON CONFLICT (machine_id, time)
        DO NOTHING;
    """

    values = (
        measured_at,
        data["machineId"],
        data["isRunning"],
        data["bowlRpm"],
        data["motorCurrent"],
        data["bearingTemp"],
        data["vibration"],
        data["tireWear"],
        data["alarmLevel"],
        data["alarmMessage"],
    )

    async with db_pool.connection() as connection:
        await connection.execute(query, values)

async def get_recent_sensor_history(
    machine_id: str,
    minutes: int,
    limit: int,
) -> list[dict[str, Any]]:
    """현재 시각을 기준으로 최근 센서 이력을 조회한다."""

    query = """
        SELECT
            time,
            machine_id,
            is_running,
            bowl_rpm,
            motor_current,
            bearing_temp,
            vibration,
            tire_wear,
            alarm_level,
            alarm_message
        FROM sensor_readings
        WHERE machine_id = %s
          AND time >= NOW() - (%s * INTERVAL '1 minute')
        ORDER BY time ASC
        LIMIT %s;
    """

    async with db_pool.connection() as connection:
        cursor = await connection.execute(
            query,
            (machine_id, minutes, limit),
        )
        rows = await cursor.fetchall()

    return rows


async def get_sensor_history_range(
    machine_id: str,
    start_time: datetime,
    end_time: datetime,
    limit: int,
) -> list[dict[str, Any]]:
    """지정된 시작·종료 시각 사이의 센서 이력을 조회한다."""

    query = """
        SELECT
            time,
            machine_id,
            is_running,
            bowl_rpm,
            motor_current,
            bearing_temp,
            vibration,
            tire_wear,
            alarm_level,
            alarm_message
        FROM sensor_readings
        WHERE machine_id = %s
          AND time >= %s
          AND time <= %s
        ORDER BY time ASC
        LIMIT %s;
    """

    async with db_pool.connection() as connection:
        cursor = await connection.execute(
            query,
            (machine_id, start_time, end_time, limit),
        )
        rows = await cursor.fetchall()

    return rows


async def get_recent_alarm_history(
    machine_id: str,
    hours: int,
    limit: int,
) -> list[dict[str, Any]]:
    """실제로 생성된 알람 이벤트 이력을 조회한다."""

    query = """
        SELECT
            started_at AS time,
            machine_id,

            start_bearing_temp AS bearing_temp,
            start_vibration AS vibration,
            start_tire_wear AS tire_wear,

            alarm_level,
            message AS alarm_message,

            ended_at,

            peak_bearing_temp,
            peak_vibration,
            peak_tire_wear

        FROM alarm_events

        WHERE machine_id = %s
          AND started_at >=
              NOW() - (%s * INTERVAL '1 hour')

        ORDER BY started_at DESC
        LIMIT %s;
    """

    async with db_pool.connection() as connection:
        cursor = await connection.execute(
            query,
            (
                machine_id,
                hours,
                limit,
            ),
        )

        rows = await cursor.fetchall()

    return rows

async def get_sensor_aggregates(
    machine_id: str,
    minutes: int,
    bucket_interval: str,
) -> list[dict[str, Any]]:
    """센서값을 지정된 시간 간격으로 평균·최댓값 집계한다."""

    query = """
        SELECT
            time_bucket(%s::INTERVAL, time) AS bucket,

            ROUND(AVG(bowl_rpm)::NUMERIC, 2) AS bowl_rpm_avg,
            ROUND(MAX(bowl_rpm)::NUMERIC, 2) AS bowl_rpm_max,

            ROUND(AVG(motor_current)::NUMERIC, 2)
                AS motor_current_avg,
            ROUND(MAX(motor_current)::NUMERIC, 2)
                AS motor_current_max,

            ROUND(AVG(bearing_temp)::NUMERIC, 2)
                AS bearing_temp_avg,
            ROUND(MAX(bearing_temp)::NUMERIC, 2)
                AS bearing_temp_max,

            ROUND(AVG(vibration)::NUMERIC, 2)
                AS vibration_avg,
            ROUND(MAX(vibration)::NUMERIC, 2)
                AS vibration_max,

            ROUND(AVG(tire_wear)::NUMERIC, 2)
                AS tire_wear_avg,

            CASE
                WHEN COUNT(*) FILTER (
                    WHERE alarm_level = 'ALARM'
                ) > 0 THEN 'ALARM'

                WHEN COUNT(*) FILTER (
                    WHERE alarm_level = 'WARNING'
                ) > 0 THEN 'WARNING'

                ELSE 'NORMAL'
            END AS alarm_level,

            COUNT(*) AS sample_count
        FROM sensor_readings
        WHERE machine_id = %s
          AND time >= NOW() - (%s * INTERVAL '1 minute')
        GROUP BY bucket
        ORDER BY bucket ASC;
    """

    async with db_pool.connection() as connection:
        cursor = await connection.execute(
            query,
            (bucket_interval, machine_id, minutes),
        )
        rows = await cursor.fetchall()

    return rows

async def create_alarm_event(
    data: dict[str, Any],
) -> None:
    """새로운 WARNING 또는 ALARM 이벤트를 생성한다."""

    started_at = datetime.fromisoformat(
        data["timestamp"]
    )

    query = """
        INSERT INTO alarm_events
        (
            machine_id,
            alarm_level,
            started_at,
            message,

            start_bearing_temp,
            start_vibration,
            start_tire_wear,

            peak_bearing_temp,
            peak_vibration,
            peak_tire_wear
        )
        VALUES
        (
            %s, %s, %s, %s,
            %s, %s, %s,
            %s, %s, %s
        );
    """

    values = (
        data["machineId"],
        data["alarmLevel"],
        started_at,
        data["alarmMessage"],

        data["bearingTemp"],
        data["vibration"],
        data["tireWear"],

        data["bearingTemp"],
        data["vibration"],
        data["tireWear"],
    )

    async with db_pool.connection() as connection:
        await connection.execute(query, values)


async def close_active_alarm_event(
    machine_id: str,
    ended_at: datetime,
) -> None:
    """현재 활성화된 알람 이벤트를 종료한다."""

    query = """
        UPDATE alarm_events
        SET ended_at = %s
        WHERE machine_id = %s
          AND ended_at IS NULL;
    """

    async with db_pool.connection() as connection:
        await connection.execute(
            query,
            (
                ended_at,
                machine_id,
            ),
        )


async def update_active_alarm_event_peaks(
    data: dict[str, Any],
) -> None:
    """알람 진행 중 최고 센서값을 갱신한다."""

    query = """
        UPDATE alarm_events
        SET
            peak_bearing_temp = GREATEST(
                peak_bearing_temp,
                %s
            ),

            peak_vibration = GREATEST(
                peak_vibration,
                %s
            ),

            peak_tire_wear = GREATEST(
                peak_tire_wear,
                %s
            )

        WHERE machine_id = %s
          AND ended_at IS NULL;
    """

    values = (
        data["bearingTemp"],
        data["vibration"],
        data["tireWear"],
        data["machineId"],
    )

    async with db_pool.connection() as connection:
        await connection.execute(query, values)