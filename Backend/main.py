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
from fastapi.middleware.cors import CORSMiddleware
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

import wear_engine

SIMULATION_MACHINE_ID = "PV-001"
UPDATE_INTERVAL_SECONDS = 0.5
ALLOWED_BUCKETS = {
    "10s": "10 seconds",
    "30s": "30 seconds",
    "1m": "1 minute",
    "5m": "5 minutes",
    "15m": "15 minutes",
}

# 롤타이어는 3개가 물리적으로 존재하지만, 실제 현장에서는 그 중 1개에만 센서를
# 달아 측정하고 마모/교체 시점도 3개를 한꺼번에 판단한다 - 그래서 대표 타이어 1개만 다룬다.
MONITORED_TIRE_ID = "roll_tire"

# 실제 누적 운전시간 데이터가 없으므로, 데모/파이프라인 검증용으로 시간을 가속한다.
# (1 real second = 1 simulated operating hour). 실데이터 연결 시 이 시뮬레이션은 제거한다.
BASELINE_CUMULATIVE_HOURS = 4000.0
SIMULATED_HOURS_PER_SECOND = 1.0

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

    # ---- 입력데이터셋 설명_미분기_20260604.xlsx 의 14개 실시간 태그 (시뮬레이션) ----
    # 실제 DCS 터널 연결 전까지 사용하는 목데이터다. 필드명은 데이터셋 문서의
    # "데이터 컬럼명"을 camelCase로 옮긴 것이며, 실데이터 연동 시에도 유지한다.
    feed_rate_tph = 42.0 + math.sin(elapsed * 0.08) * 8.0
    mw = 480.0 + math.sin(elapsed * 0.05) * 40.0
    inlet_temp_c = 260.0 + math.sin(elapsed * 0.15) * 15.0
    outlet_temp_c = 78.0 + math.sin(elapsed * 0.15) * 4.0
    bowl_dp_mmh2o = 250.0 + math.sin(elapsed * 0.2) * 30.0 + (feed_rate_tph - 42.0) * 2.0
    classifier_speed_demand_pct = 55.0 + math.sin(elapsed * 0.12) * 10.0
    classifier_vane_speed_actual_rpm = 95.0 + math.sin(elapsed * 0.12) * 15.0
    classifier_speed_bias_pct = math.sin(elapsed * 0.07) * 5.0
    pyrite_valve_open = int(elapsed) % 45 < 3
    pyrite_hopper_temp_c = 60.0 + math.sin(elapsed * 0.1) * 5.0
    journal_pressure_setpoint_psi = 900.0
    journal_pressure_actual_psi = 900.0 + math.sin(elapsed * 0.3) * 40.0

    cumulative_operating_hours = (
        BASELINE_CUMULATIVE_HOURS + elapsed * SIMULATED_HOURS_PER_SECOND
    )

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

        # 아래부터는 실제 데이터셋 문서(14개 태그) 기준 필드 - 신규 추가.
        "mw": round(mw, 1),
        "feedRateTph": round(feed_rate_tph, 1),
        "inletTempC": round(inlet_temp_c, 1),
        "outletTempC": round(outlet_temp_c, 1),
        "bowlDpMmH2o": round(bowl_dp_mmh2o, 1),
        "classifierSpeedDemandPct": round(classifier_speed_demand_pct, 1),
        "classifierVaneSpeedActualRpm": round(classifier_vane_speed_actual_rpm, 1),
        "classifierSpeedBiasPct": round(classifier_speed_bias_pct, 1),
        "pyriteValveOpen": pyrite_valve_open,
        "pyriteHopperTempC": round(pyrite_hopper_temp_c, 1),
        "journalPressureSetpointPsi": round(journal_pressure_setpoint_psi, 1),
        "journalPressureActualPsi": round(journal_pressure_actual_psi, 1),
        "cumulativeOperatingHours": round(cumulative_operating_hours, 1),
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

# 프론트엔드(pulverizer-frontend, Vite 개발서버) 및 Unity WebGL 임베드 호스트에서의
# 브라우저 요청을 허용한다. 실서비스 배포 시에는 실제 프론트 도메인으로 좁혀야 한다.
app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:3142",
        "http://127.0.0.1:3142",
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
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

@app.get("/api/pulverizers/{machine_id}/wear")
async def get_pulverizer_wear(
    machine_id: str,
    contact_width_scenario: str = Query(
        default=wear_engine.DEFAULT_CONTACT_WIDTH_SCENARIO,
        description=(
            "유효 접촉폭 시나리오: "
            + ", ".join(wear_engine.CONTACT_WIDTH_SCENARIOS_M)
        ),
    ),
    wear_coefficient_k: float | None = Query(
        default=None,
        description="마모계수 K 오버라이드 (기본값: 가정값 사용, what-if 분석용)",
    ),
    load_newtons: float | None = Query(
        default=None,
        description="롤 1개당 하중 F(N) 수동 오버라이드 (what-if 분석용)",
    ),
) -> dict[str, Any]:
    """물리식(Archard 법칙) 기반 롤타이어 마모량/잔존수명을 계산한다.

    실제 현장에서는 롤타이어 3개 중 1개에만 센서(Journal Pressure 등)를 달아
    측정하고, 교체 시점이 오면 3개를 한꺼번에 교체한다 - 그래서 API도 타이어
    3개가 아니라 대표 롤타이어 1개에 대한 결과만 반환한다.

    계산은 항상 이 엔드포인트(FastAPI)에서만 수행한다 - Unity/React는 결과만 표시한다.
    """

    data = latest_status.get(machine_id)

    if data is None:
        raise HTTPException(
            status_code=404,
            detail="Machine data not found",
        )

    cumulative_hours = data.get(
        "cumulativeOperatingHours",
        BASELINE_CUMULATIVE_HOURS,
    )
    journal_pressure_actual = data.get("journalPressureActualPsi")

    k_value = (
        wear_coefficient_k
        if wear_coefficient_k is not None
        else wear_engine.DEFAULT_WEAR_COEFFICIENT_K
    )

    result = wear_engine.compute_wear(
        tire_id=MONITORED_TIRE_ID,
        cumulative_hours=cumulative_hours,
        journal_pressure_actual_psi=(
            journal_pressure_actual if load_newtons is None else None
        ),
        wear_coefficient_k=k_value,
        contact_width_scenario=contact_width_scenario,
        load_newtons_override=load_newtons,
    )

    return {
        "machineId": machine_id,
        "equation": "h = C(t) x K x F  (Archard 법칙 기반)",
        "equationConstants": {
            "bowlTrackDiameterM": wear_engine.BOWL_TRACK_DIAMETER_M,
            "bowlRpm": wear_engine.BOWL_RPM,
            "overlayHardnessPa": wear_engine.OVERLAY_HARDNESS_PA,
            "contactLengthM": wear_engine.CONTACT_LENGTH_M,
            "wearLimitMm": wear_engine.WEAR_LIMIT_MM,
        },
        "contactWidthScenarios": wear_engine.CONTACT_WIDTH_SCENARIOS_M,
        "tire": {
            "tireId": result.tire_id,
            "cumulativeHours": round(result.cumulative_hours, 1),
            "contactWidthScenario": result.contact_width_scenario,
            "wearCoefficientK": result.wear_coefficient_k,
            "loadNewtons": round(result.load_newtons, 1),
            "loadEstimationMethod": result.load_estimation_method,
            "amplificationC": round(result.amplification_c, 4),
            "wearMm": result.wear_mm,
            "wearLimitMm": result.wear_limit_mm,
            "wearRateMmPerHour": result.wear_rate_mm_per_hour,
            "remainingUsefulLifeHours": result.remaining_useful_life_hours,
            "damagePercent": result.damage_percent,
            "severityMultiplier": result.severity_multiplier,
            "notes": result.notes,
            "zoneWearProfileMm": result.zone_wear_profile_mm,
        },
    }


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
