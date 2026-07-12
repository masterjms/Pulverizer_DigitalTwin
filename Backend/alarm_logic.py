from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class AlarmTransition:
    """알람 상태 변경 결과."""

    previous_level: str
    current_level: str
    changed: bool


class AlarmStateMachine:
    """센서값이 일정 시간 유지될 때만 알람 상태를 변경한다."""

    def __init__(
        self,
        stable_seconds: float = 5.0,
    ) -> None:
        self.stable_seconds = stable_seconds

        self.current_level = "NORMAL"
        self.candidate_level = "NORMAL"
        self.candidate_since = time.monotonic()

    def evaluate(
        self,
        data: dict[str, Any],
    ) -> AlarmTransition:
        desired_level = self._evaluate_desired_level(data)
        current_time = time.monotonic()

        # 현재 상태와 같으면 상태 변경 후보를 초기화한다.
        if desired_level == self.current_level:
            self.candidate_level = desired_level
            self.candidate_since = current_time

            return AlarmTransition(
                previous_level=self.current_level,
                current_level=self.current_level,
                changed=False,
            )

        # 새로운 후보 상태가 나타난 시점을 저장한다.
        if desired_level != self.candidate_level:
            self.candidate_level = desired_level
            self.candidate_since = current_time

            return AlarmTransition(
                previous_level=self.current_level,
                current_level=self.current_level,
                changed=False,
            )

        candidate_duration = (
            current_time - self.candidate_since
        )

        # 5초 동안 유지되지 않았으면 현재 상태를 유지한다.
        if candidate_duration < self.stable_seconds:
            return AlarmTransition(
                previous_level=self.current_level,
                current_level=self.current_level,
                changed=False,
            )

        previous_level = self.current_level

        self.current_level = self.candidate_level
        self.candidate_since = current_time

        return AlarmTransition(
            previous_level=previous_level,
            current_level=self.current_level,
            changed=True,
        )

    def _evaluate_desired_level(
        self,
        data: dict[str, Any],
    ) -> str:
        temperature = float(data["bearingTemp"])
        vibration = float(data["vibration"])
        tire_wear = float(data["tireWear"])

        # ALARM 상태에서는 낮은 해제 기준을 사용한다.
        if self.current_level == "ALARM":
            if temperature >= 82.0 or vibration >= 6.5:
                return "ALARM"

            if (
                temperature >= 70.0
                or vibration >= 4.0
                or tire_wear >= 60.0
            ):
                return "WARNING"

            return "NORMAL"

        # WARNING 상태에서도 해제 기준을 조금 낮게 둔다.
        if self.current_level == "WARNING":
            if temperature >= 85.0 or vibration >= 7.0:
                return "ALARM"

            if (
                temperature >= 68.0
                or vibration >= 3.5
                or tire_wear >= 55.0
            ):
                return "WARNING"

            return "NORMAL"

        # NORMAL 상태의 진입 기준
        if temperature >= 85.0 or vibration >= 7.0:
            return "ALARM"

        if (
            temperature >= 70.0
            or vibration >= 4.0
            or tire_wear >= 60.0
        ):
            return "WARNING"

        return "NORMAL"


def get_alarm_message(level: str) -> str:
    """알람 단계에 맞는 표시 문구를 반환한다."""

    if level == "ALARM":
        return "Immediate equipment inspection required."

    if level == "WARNING":
        return "Equipment condition requires monitoring."

    return ""