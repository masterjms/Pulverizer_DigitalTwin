"""
롤타이어 마모방정식 (Archard 법칙 기반) 계산 모듈.

h = C(t) x K x F
C(t) = pi * D * RPM * 60 * t / (H * A) * 1000      [mm/N]

D, RPM, H, L은 장비 스펙에서 확정된 값이다 (붙임1/4/6 문서, 2026-07 확인).
K(마모계수), F(롤 1개당 법선하중), w(유효 접촉폭)는 아직 확정값이 아니다 - 실측
마모 데이터(정비이력)가 확보되기 전까지는 설정 가능한 파라미터/시나리오로만 다룬다.
이 값들을 코드에 "정답"처럼 하드코딩하지 말 것.

이 모듈은 백엔드에서만 사용한다. Unity/React는 이 계산을 복제하지 않고 API 응답을
그대로 표시한다.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field

# ---- 확정된 장비 상수 (붙임1 재질검사 성적서 / 붙임4 자재규격서 / 붙임6 미분기 스펙) ----
BOWL_TRACK_DIAMETER_M = 2.413       # D: 보울 트랙 지름
BOWL_RPM = 41.6                     # 보울 회전수 (모터 882rpm / 감속비 21.6)
OVERLAY_HARDNESS_PA = 9.2e9         # H: 65 HRC 환산
CONTACT_LENGTH_M = 0.335            # L: 접촉 길이
WEAR_LIMIT_MM = 38.0                # 반경 기준 교체 한계 (신품 1422mm -> 교체 1346mm)
ROLL_FULL_WIDTH_M = 0.483           # 롤 전폭 (w 시나리오 ②의 기준값)

# w(유효 접촉폭) 시나리오 - 마모방정식 설명 문서의 3가지 관점. 아직 어느 것이 맞는지
# 확정되지 않았으므로 선택 가능한 옵션으로 둔다.
CONTACT_WIDTH_SCENARIOS_M: dict[str, float] = {
    "band": 0.0127,                          # ① 밴드 집중 (요청데이터 문서 기재값, 원출처 확인 필요)
    "full_width": ROLL_FULL_WIDTH_M,          # ② 전폭 분산
    "partial_sweep_30pct": ROLL_FULL_WIDTH_M * 0.3,  # ③ 부분 스윕 (예시 c=0.3)
}
DEFAULT_CONTACT_WIDTH_SCENARIO = "band"

# K(마모계수) 기본값 - 설명 문서 계산기의 초기 슬라이더 값(10^-7.15)과 동일하게 맞춤.
# 실측 마모(정비이력)가 확보되면 이 값을 역산해서 갱신해야 한다 - 지금은 "가정값"이다.
DEFAULT_WEAR_COEFFICIENT_K = 10 ** (-71.5 / 10)

# F(하중, N) 기본값 - 설명 문서 계산기의 초기 슬라이더 값(50kN)과 동일하게 맞춤.
DEFAULT_LOAD_NEWTONS = 50_000.0

# ---- F 추정: Journal Pressure를 하중의 프록시로 사용 (잠정) ----
# 마모방정식 문서의 "경로1 유압센서 기반" 계산에는 실린더 보어(in)와 실린더 개수가
# 필요한데 아직 확보되지 않았다. 이 프로젝트에서는 사용자 확인 하에, 데이터셋의
# Journal Pressure(현재값)가 롤 가압 유압 계통과 같다고 "잠정 가정"하고, 기준압력
# 대비 비율만큼 기본 하중(F_baseline)을 스케일링하는 방식으로 근사한다.
# 실린더 보어/개수 자료가 확보되면 이 함수를 실제 경로1 공식으로 교체해야 한다.
JOURNAL_PRESSURE_REFERENCE_PSI = 800.0


def estimate_load_from_journal_pressure(
    journal_pressure_actual_psi: float,
    baseline_load_n: float = DEFAULT_LOAD_NEWTONS,
    reference_psi: float = JOURNAL_PRESSURE_REFERENCE_PSI,
) -> float:
    """Journal Pressure 현재값으로 하중 F(N)을 근사한다 (잠정 - 실린더 제원 미확보)."""

    if reference_psi <= 0:
        return baseline_load_n

    scale = journal_pressure_actual_psi / reference_psi
    return baseline_load_n * scale


@dataclass
class WearResult:
    tire_id: str
    cumulative_hours: float
    contact_width_scenario: str
    contact_width_m: float
    wear_coefficient_k: float
    load_newtons: float
    amplification_c: float          # C(t), mm/N
    wear_mm: float                  # h
    wear_limit_mm: float = WEAR_LIMIT_MM
    wear_rate_mm_per_hour: float = 0.0
    remaining_useful_life_hours: float | None = None
    load_estimation_method: str = "provisional_default"
    notes: list[str] = field(default_factory=list)
    # Zone 1(내측 좁은 끝) ~ Zone N(외측 넓은 끝) 마모 분포 - 실측 아님, 전체 평균 +
    # 접촉폭 시나리오 형태로부터 만든 예시적 분포 (compute_zone_wear_profile 참고).
    zone_wear_profile_mm: list[float] = field(default_factory=list)
    # 손상도(%) = 마모량/한계치. 심각도 배수 = 현재 추정 하중 / 기본 하중(50kN) -
    # 현재 운전조건이 기준 대비 얼마나 더 가혹한지 보여주는 파생값 (실측 보정계수 아님).
    damage_percent: float = 0.0
    severity_multiplier: float = 1.0


def compute_amplification_c(
    cumulative_hours: float,
    contact_width_m: float,
    contact_length_m: float = CONTACT_LENGTH_M,
    diameter_m: float = BOWL_TRACK_DIAMETER_M,
    rpm: float = BOWL_RPM,
    hardness_pa: float = OVERLAY_HARDNESS_PA,
) -> float:
    """C(t) = pi*D*RPM*60*t / (H*A) * 1000  [mm/N]"""

    contact_area_m2 = contact_width_m * contact_length_m

    if contact_area_m2 <= 0:
        raise ValueError("contact_width_m과 contact_length_m은 0보다 커야 합니다.")

    rolled_distance_m = math.pi * diameter_m * rpm * 60.0 * cumulative_hours

    return rolled_distance_m / (hardness_pa * contact_area_m2) * 1000.0


ZONE_COUNT = 10

# 시나리오별 마모 분포 폭(zone 단위 표준편차). band는 좁은 구간에 집중, full_width는
# 사실상 평탄하게 퍼짐 - 마모방정식 설명 문서의 3가지 접촉폭 관점과 대응시킨다.
ZONE_SPREAD_BY_SCENARIO: dict[str, float] = {
    "band": 1.0,
    "partial_sweep_30pct": 2.2,
    "full_width": 6.0,
}


def compute_zone_wear_profile(
    overall_wear_mm: float,
    contact_width_scenario: str,
    num_zones: int = ZONE_COUNT,
) -> list[float]:
    """롤타이어 단면(Zone 1=내측 좁은 끝 ~ Zone N=외측 넓은 끝)의 마모 분포를 만든다.

    주의: 존별 센서가 없어서 이 분포는 "전체 평균 마모량 + 접촉폭 시나리오 형태"로부터
    만든 예시적(synthetic) 분포다 - 실측이 아니다. 평균은 항상 overall_wear_mm과 같도록
    정규화해서, 적어도 집계값과는 항상 일치하게 만든다.
    """

    sigma = ZONE_SPREAD_BY_SCENARIO.get(contact_width_scenario, 2.0)
    center = (num_zones + 1) / 2.0

    weights = [
        math.exp(-((zone - center) ** 2) / (2 * sigma * sigma))
        for zone in range(1, num_zones + 1)
    ]
    mean_weight = sum(weights) / len(weights)

    return [round(overall_wear_mm * (w / mean_weight), 2) for w in weights]


def compute_wear(
    tire_id: str,
    cumulative_hours: float,
    journal_pressure_actual_psi: float | None = None,
    wear_coefficient_k: float = DEFAULT_WEAR_COEFFICIENT_K,
    contact_width_scenario: str = DEFAULT_CONTACT_WIDTH_SCENARIO,
    load_newtons_override: float | None = None,
) -> WearResult:
    """단일 타이어의 마모량(h)과 잔존수명(RUL)을 계산한다."""

    if contact_width_scenario not in CONTACT_WIDTH_SCENARIOS_M:
        raise ValueError(
            f"알 수 없는 contact_width_scenario: {contact_width_scenario} "
            f"(가능한 값: {list(CONTACT_WIDTH_SCENARIOS_M)})"
        )

    contact_width_m = CONTACT_WIDTH_SCENARIOS_M[contact_width_scenario]
    notes: list[str] = []

    if load_newtons_override is not None:
        load_newtons = load_newtons_override
        load_estimation_method = "manual_override"
    elif journal_pressure_actual_psi is not None:
        load_newtons = estimate_load_from_journal_pressure(journal_pressure_actual_psi)
        load_estimation_method = "journal_pressure_proxy (provisional - cylinder bore/count not confirmed)"
        notes.append(
            "Journal Pressure를 롤 가압 유압계통과 동일하다고 가정한 잠정 추정치입니다."
        )
    else:
        load_newtons = DEFAULT_LOAD_NEWTONS
        load_estimation_method = "default_constant"

    amplification_c = compute_amplification_c(cumulative_hours, contact_width_m)
    wear_mm = amplification_c * wear_coefficient_k * load_newtons

    wear_rate_mm_per_hour = wear_mm / cumulative_hours if cumulative_hours > 0 else 0.0

    remaining_hours: float | None
    if wear_rate_mm_per_hour > 0:
        remaining_hours = max((WEAR_LIMIT_MM - wear_mm) / wear_rate_mm_per_hour, 0.0)
    else:
        remaining_hours = None

    if contact_width_scenario == "band":
        notes.append(
            "w=12.7mm 시나리오는 접촉폭 3가지 관점 중 하나이며, 실제 마모 패턴 확인 전까지는 "
            "다른 시나리오(full_width, partial_sweep_30pct)와 최대 38배 차이가 날 수 있습니다."
        )
    notes.append("K, F는 실측 마모 데이터로 보정되기 전까지는 가정값입니다.")
    notes.append(
        "Zone별(1~10) 마모 분포는 존별 센서가 없어 전체 평균과 접촉폭 시나리오로부터 "
        "만든 예시 분포입니다 - 실측값이 아닙니다."
    )

    zone_wear_profile_mm = compute_zone_wear_profile(wear_mm, contact_width_scenario)
    damage_percent = (wear_mm / WEAR_LIMIT_MM) * 100.0 if WEAR_LIMIT_MM > 0 else 0.0
    severity_multiplier = load_newtons / DEFAULT_LOAD_NEWTONS if DEFAULT_LOAD_NEWTONS > 0 else 1.0

    return WearResult(
        tire_id=tire_id,
        cumulative_hours=cumulative_hours,
        contact_width_scenario=contact_width_scenario,
        contact_width_m=contact_width_m,
        wear_coefficient_k=wear_coefficient_k,
        load_newtons=load_newtons,
        amplification_c=amplification_c,
        wear_mm=round(wear_mm, 3),
        wear_rate_mm_per_hour=round(wear_rate_mm_per_hour, 5),
        remaining_useful_life_hours=(
            round(remaining_hours, 1) if remaining_hours is not None else None
        ),
        damage_percent=round(damage_percent, 1),
        severity_multiplier=round(severity_multiplier, 2),
        load_estimation_method=load_estimation_method,
        notes=notes,
        zone_wear_profile_mm=zone_wear_profile_mm,
    )
