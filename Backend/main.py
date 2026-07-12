from __future__ import annotations

import asyncio
import contextlib
import math
import time
from collections import defaultdict
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from typing import Any

from fastapi import (
    FastAPI,
    HTTPException,
    Query,
    WebSocket,
    WebSocketDisconnect,
)
from fastapi.responses import HTMLResponse

from database import (
    close_active_alarm_event,
    close_database,
    create_alarm_event,
    database_is_ready,
    get_recent_alarm_history,
    get_recent_sensor_history,
    get_sensor_aggregates,
    get_sensor_history_range,
    open_database,
    save_sensor_reading,
    update_active_alarm_event_peaks,
)

from alarm_logic import (
    AlarmStateMachine,
    get_alarm_message,
)

SIMULATION_MACHINE_ID = "PV-001"
UPDATE_INTERVAL_SECONDS = 0.5
ALLOWED_BUCKETS = {
    "10s": "10 seconds",
    "30s": "30 seconds",
    "1m": "1 minute",
    "5m": "5 minutes",
    "15m": "15 minutes",
}

START_TIME = time.monotonic()

# 설비별 가장 최근 상태를 메모리에 보관한다.
latest_status: dict[str, dict[str, Any]] = {}


class ConnectionManager:
    """설비별 WebSocket 접속자를 관리한다."""

    def __init__(self) -> None:
        self.connections: dict[str, list[WebSocket]] = defaultdict(list)

    async def connect(
        self,
        machine_id: str,
        websocket: WebSocket,
    ) -> None:
        await websocket.accept()
        self.connections[machine_id].append(websocket)

    def disconnect(
        self,
        machine_id: str,
        websocket: WebSocket,
    ) -> None:
        machine_connections = self.connections.get(machine_id)

        if not machine_connections:
            return

        if websocket in machine_connections:
            machine_connections.remove(websocket)

        if not machine_connections:
            self.connections.pop(machine_id, None)

    async def broadcast(
        self,
        machine_id: str,
        data: dict[str, Any],
    ) -> None:
        machine_connections = self.connections.get(machine_id, [])

        if not machine_connections:
            return

        disconnected_clients: list[WebSocket] = []

        # 전송 중 목록이 바뀌지 않도록 복사본을 순회한다.
        for websocket in list(machine_connections):
            try:
                await websocket.send_json(data)

            except Exception:
                disconnected_clients.append(websocket)

        for websocket in disconnected_clients:
            self.disconnect(machine_id, websocket)


connection_manager = ConnectionManager()
alarm_state_machine = AlarmStateMachine(
    stable_seconds=5.0
)


def create_pulverizer_data(machine_id: str) -> dict[str, Any]:
    """현재 시뮬레이션 시간에 해당하는 미분기 데이터를 만든다."""

    elapsed = time.monotonic() - START_TIME

    # 20초 단위로 NORMAL → WARNING → ALARM을 반복한다.
    phase = int(elapsed // 20.0) % 3

    bowl_rpm = 42.0 + math.sin(elapsed * 0.7) * 2.5
    motor_current = 23.4 + math.sin(elapsed * 0.5) * 3.0
    tire_wear = 34.0 + math.sin(elapsed * 0.1) * 2.0

    if phase == 0:
        bearing_temp = 66.0 + math.sin(elapsed * 0.3) * 2.0
        vibration = 3.2 + math.sin(elapsed * 0.8) * 0.4

    elif phase == 1:
        bearing_temp = 74.0 + math.sin(elapsed * 0.3) * 2.0
        vibration = 5.0 + math.sin(elapsed * 0.8) * 0.5

    else:
        bearing_temp = 88.0 + math.sin(elapsed * 0.3) * 2.0
        vibration = 7.5 + math.sin(elapsed * 0.8) * 0.4

    alarm_level = "NORMAL"
    alarm_message = ""

    return {
        # UTC 기준 측정 시각이다.
        # Unity의 기존 PulverizerData에 이 필드가 없어도 무시되므로 문제없다.
        "timestamp": datetime.now(timezone.utc).isoformat(),

        "machineId": machine_id,
        "isRunning": True,

        "bowlRpm": round(bowl_rpm, 1),
        "motorCurrent": round(motor_current, 1),
        "bearingTemp": round(bearing_temp, 1),
        "vibration": round(vibration, 1),
        "tireWear": round(tire_wear, 1),

        "alarmLevel": alarm_level,
        "alarmMessage": alarm_message,
    }


async def sensor_simulation_loop() -> None:
    """센서 저장, 알람 판정, 이벤트 기록, Viewer 전송을 수행한다."""

    while True:
        data = create_pulverizer_data(
            SIMULATION_MACHINE_ID
        )

        transition = alarm_state_machine.evaluate(data)

        data["alarmLevel"] = transition.current_level
        data["alarmMessage"] = get_alarm_message(
            transition.current_level
        )

        latest_status[SIMULATION_MACHINE_ID] = data

        try:
            await save_sensor_reading(data)

        except Exception as exception:
            print(
                f"[DB ERROR] Sensor save failed: "
                f"{exception}"
            )

        try:
            measured_at = datetime.fromisoformat(
                data["timestamp"]
            )

            if transition.changed:
                print(
                    "[ALARM STATE] "
                    f"{transition.previous_level}"
                    " -> "
                    f"{transition.current_level}"
                )

                # 기존 WARNING 또는 ALARM 이벤트 종료
                if transition.previous_level != "NORMAL":
                    await close_active_alarm_event(
                        SIMULATION_MACHINE_ID,
                        measured_at,
                    )

                # 새로운 WARNING 또는 ALARM 이벤트 생성
                if transition.current_level != "NORMAL":
                    await create_alarm_event(data)

            elif transition.current_level != "NORMAL":
                await update_active_alarm_event_peaks(
                    data
                )

        except Exception as exception:
            print(
                f"[DB ERROR] Alarm event failed: "
                f"{exception}"
            )

        await connection_manager.broadcast(
            SIMULATION_MACHINE_ID,
            data,
        )

        await asyncio.sleep(
            UPDATE_INTERVAL_SECONDS
        )


@asynccontextmanager
async def lifespan(app: FastAPI):
    """FastAPI 시작과 종료 시 필요한 자원을 관리한다."""

    await open_database()

    await close_active_alarm_event(
    SIMULATION_MACHINE_ID,
    datetime.now(timezone.utc),
)

    simulation_task = asyncio.create_task(
        sensor_simulation_loop()
    )

    print("[SERVER] Sensor simulation started")

    try:
        yield

    finally:
        simulation_task.cancel()

        with contextlib.suppress(asyncio.CancelledError):
            await simulation_task

        await close_database()

        print("[SERVER] Shutdown complete")


app = FastAPI(
    title="Coal Pulverizer Digital Twin Server",
    version="2.0.0",
    lifespan=lifespan,
)

@app.get("/api/pulverizers/{machine_id}/aggregates")
async def read_sensor_aggregates(
    machine_id: str,
    minutes: int = Query(
        default=60,
        ge=1,
        le=10080,
        description="집계 대상 최근 시간 범위",
    ),
    bucket: str = Query(
        default="1m",
        description="집계 간격: 10s, 30s, 1m, 5m, 15m",
    ),
) -> dict[str, Any]:
    """TimescaleDB time_bucket을 사용한 센서 집계 결과를 반환한다."""

    bucket_interval = ALLOWED_BUCKETS.get(bucket)

    if bucket_interval is None:
        raise HTTPException(
            status_code=400,
            detail="bucket은 10s, 30s, 1m, 5m, 15m 중 하나여야 합니다.",
        )

    rows = await get_sensor_aggregates(
        machine_id=machine_id,
        minutes=minutes,
        bucket_interval=bucket_interval,
    )

    return {
        "machineId": machine_id,
        "minutes": minutes,
        "bucket": bucket,
        "count": len(rows),
        "data": rows,
    }

@app.get("/health")
async def health() -> dict[str, str]:
    """FastAPI와 TimescaleDB 상태를 함께 확인한다."""

    database_status = (
        "ok"
        if await database_is_ready()
        else "error"
    )

    return {
        "server": "ok",
        "database": database_status,
    }


@app.get("/api/pulverizers/{machine_id}/status")
async def get_pulverizer_status(
    machine_id: str,
) -> dict[str, Any]:
    """서버가 보관 중인 가장 최근 설비 상태를 조회한다."""

    data = latest_status.get(machine_id)

    if data is None:
        raise HTTPException(
            status_code=404,
            detail="Machine data not found",
        )

    return data

@app.get("/api/pulverizers/{machine_id}/history/range")
async def read_history_range(
    machine_id: str,
    start: datetime = Query(
        description="조회 시작 시각. ISO 8601 형식",
    ),
    end: datetime = Query(
        description="조회 종료 시각. ISO 8601 형식",
    ),
    limit: int = Query(
        default=5000,
        ge=1,
        le=20000,
    ),
) -> dict[str, Any]:
    """지정된 시작·종료 시각 사이의 센서 데이터를 반환한다."""

    if start >= end:
        raise HTTPException(
            status_code=400,
            detail="start는 end보다 이전 시각이어야 합니다.",
        )

    rows = await get_sensor_history_range(
        machine_id=machine_id,
        start_time=start,
        end_time=end,
        limit=limit,
    )

    return {
        "machineId": machine_id,
        "start": start,
        "end": end,
        "count": len(rows),
        "data": rows,
    }

@app.get("/api/pulverizers/{machine_id}/history")
async def read_recent_history(
    machine_id: str,
    minutes: int = Query(
        default=10,
        ge=1,
        le=1440,
        description="현재 시각 기준 조회할 최근 분 단위 범위",
    ),
    limit: int = Query(
        default=1200,
        ge=1,
        le=10000,
        description="최대 반환 데이터 개수",
    ),
) -> dict[str, Any]:
    """최근 일정 시간 동안의 원본 센서 데이터를 반환한다."""

    rows = await get_recent_sensor_history(
        machine_id=machine_id,
        minutes=minutes,
        limit=limit,
    )

    return {
        "machineId": machine_id,
        "minutes": minutes,
        "count": len(rows),
        "data": rows,
    }

@app.get("/api/pulverizers/{machine_id}/alarms")
async def read_recent_alarms(
    machine_id: str,
    hours: int = Query(
        default=24,
        ge=1,
        le=720,
        description="현재 시각 기준 조회할 최근 시간",
    ),
    limit: int = Query(
        default=100,
        ge=1,
        le=1000,
    ),
) -> dict[str, Any]:
    """최근 WARNING 및 ALARM 상태의 센서 데이터를 반환한다."""

    rows = await get_recent_alarm_history(
        machine_id=machine_id,
        hours=hours,
        limit=limit,
    )

    return {
        "machineId": machine_id,
        "hours": hours,
        "count": len(rows),
        "data": rows,
    }

@app.websocket("/ws/pulverizers/{machine_id}")
async def pulverizer_websocket(
    websocket: WebSocket,
    machine_id: str,
) -> None:
    """Unity 또는 브라우저 Viewer를 WebSocket에 등록한다."""

    await connection_manager.connect(
        machine_id,
        websocket,
    )

    print(f"[CONNECTED] WebSocket client: {machine_id}")

    # 연결 직후 가장 최근 값이 있으면 즉시 한 번 보낸다.
    current_data = latest_status.get(machine_id)

    if current_data is not None:
        await websocket.send_json(current_data)

    try:
        # 클라이언트가 데이터를 보내지 않아도 연결 종료를 감지하기 위해 대기한다.
        while True:
            await websocket.receive_text()

    except WebSocketDisconnect:
        pass

    finally:
        connection_manager.disconnect(
            machine_id,
            websocket,
        )

        print(f"[DISCONNECTED] WebSocket client: {machine_id}")


TEST_PAGE = """
<!DOCTYPE html>
<html lang="ko">
<head>
    <meta charset="UTF-8">
    <title>Pulverizer WebSocket Test</title>

    <style>
        body {
            font-family: Consolas, monospace;
            background: #20252b;
            color: #e8edf2;
            padding: 30px;
        }

        #state {
            font-size: 20px;
            margin-bottom: 20px;
        }

        pre {
            background: #11161b;
            padding: 20px;
            border-radius: 8px;
            font-size: 16px;
        }
    </style>
</head>

<body>
    <h1>Coal Pulverizer WebSocket Test</h1>

    <div id="state">CONNECTING...</div>
    <pre id="data">Waiting for data...</pre>

    <script>
        const scheme =
            location.protocol === "https:" ? "wss" : "ws";

        const socket = new WebSocket(
            `${scheme}://${location.host}/ws/pulverizers/PV-001`
        );

        const state =
            document.getElementById("state");

        const dataView =
            document.getElementById("data");

        socket.onopen = () => {
            state.textContent = "CONNECTED";
            state.style.color = "#55dd88";
        };

        socket.onmessage = (event) => {
            const data = JSON.parse(event.data);

            dataView.textContent =
                JSON.stringify(data, null, 2);
        };

        socket.onerror = () => {
            state.textContent = "ERROR";
            state.style.color = "#ff6666";
        };

        socket.onclose = () => {
            state.textContent = "DISCONNECTED";
            state.style.color = "#ffcc55";
        };
    </script>
</body>
</html>
"""


@app.get("/monitor", response_class=HTMLResponse)
async def monitor() -> str:
    """브라우저에서 실시간 데이터를 확인한다."""

    return TEST_PAGE
