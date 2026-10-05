# CE883 데이터 조사 결과 — 2026-09-28

## 결론과 조사 범위

사용자 PC의 `pulverizer-lab` 스키마 생성·ETL·데모 코드, `PulverizerTwin/Backend` SQL·서버 코드, 카카오톡 받은 파일의 입력 데이터 정의서를 조사했다. **확인한 로컬 스키마에는 일일 발전 목표(MWh), 누적 발전 실적(MWh), 전력 단가, 부품·정비·정지 비용 컬럼이 없다.** 운영 서버에는 접속하지 않았으므로, 서버의 추가 테이블에도 없다고 단정할 수는 없다.

React 화면은 이 값을 임의로 채우지 않는다. 미연결 상태를 기본으로 하며, 사용자가 명시적으로 여는 가상 UI 예제에만 시험값이 들어 있다.

## 확인한 원천

| 로컬 근거 | 확인 내용 | 화면 연결 판단 |
|---|---|---|
| `C:/Users/mstot/pulverizer-lab/migrate.py`, `migrate_b.py` | `pulverizer_a`~`pulverizer_f`: Time 및 14개 센서 컬럼 | 설비별 시계열의 원천. A~F 실제 레코드 조회는 별도 필요 |
| `C:/Users/mstot/OneDrive/문서/카카오톡 받은 파일/입력데이터셋 설명_ 미분기_20260604.xlsx`, Sheet1 | 센서 의미, 상태 코드, 단위 | 아래 단위표의 기준 |
| `C:/Users/mstot/pulverizer-lab/etl_komipo_docs.py` | `wear_measurement(unit, roller, measure_date, position, wear_mm)` | 실측 마모. unit/roller와 UI 설비·롤 ID 매핑 검증 필요 |
| 같은 ETL | `coal_input`: 일자, 배합, 투입량, 수분, 발열량, 회분 등 | 연료 성상·투입량. 발전 목표나 가격은 없음 |
| `C:/Users/mstot/pulverizer-lab/twin_data.py` | `maintenance_event(id, unit, event_date, component, action, reset_factor, note)` | 정비 이력. 비용 필드는 없음 |
| `C:/Users/mstot/PulverizerTwin/Backend/db_init.sql`, `alarm_events_migration.sql` | sensor_readings, alarm_events | 별도 센서/알람 구조. Backend/main.py의 생성값은 실측으로 간주하지 않음 |
| `C:/Users/mstot/pulverizer-lab/agent_service.py` | DEMO_PLANT_THERMAL_EFF, CAPACITY_TPH, EFFICIENCY_PCT, DEMO_RUL_DAYS 및 계획 요청의 target_tonnes_per_day | 데모 상수와 요청 입력. 실제 발전 목표·효율·RUL DB 컬럼의 근거가 아님 |
| `C:/Users/mstot/pulverizer-lab/wear_sim_output/` | wear_simulation.csv, events.csv, summary.json | 출력물 출처·실측 보정 검증 전 실제 운전 기록으로 사용하지 않음 |

`wear_sim/data_loader.py`에는 DB·캐시·pickle 실패 후 합성 B 데이터를 만드는 경로가 있다. 발견한 출력은 2025-01-01 시작 2,000개 시간 행이며 summary의 calibration이 null이다. 이것만으로 실측인지 확정할 수 없어 기본 화면에 자동 연결하지 않았다.

## 원본 단위와 상태 코드

| 원본 태그 | 의미와 단위 | 정의서 행 |
|---|---|---|
| MW | 발전소 출력 MW. 미분기 6대 공통 값을 합산하면 안 됨 | 3 |
| MTR_STATUS | 기동 1, 정지 4. 그 외 코드는 별도 해석 필요 | 4 |
| MOTOR_POWER | 모터 **전류 A**. 태그 이름만 보고 kW로 계산하면 안 됨 | 5 |
| MILL_BOWL_DP | 보울 차압 mmH₂O | 8 |
| DYN_CLASSIFIER_SPEED | 실제 분급기 속도 rpm | 10 |
| MILL_JOURNAL_PR | 저널 압력 설정 psi | 14 |
| MILL_HYD_PRESS_JOURNAL | 저널 압력 실측 psi | 15 |
| FEEDER_FEEDRATE | 급탄량 t/h | 16 |

AMWI와 마모율·RUL은 원천 센서 그대로가 아니라 기존 분석 서비스의 계산 결과여야 한다. UI에 새 마모 공식을 중복 구현하지 않았다. 관행 20,000 h는 비교 기준이며 예측 수명으로 사용하지 않는다.

## 추가로 필요한 데이터

1. 발전 목표: 발전소/호기, 운영일(KST), 목표 MWh, 목표 변경 이력.
2. 실적: 같은 호기의 검증된 누적 MWh와 집계 기준 시각. MW를 적분한다면 표본 간격·누락·시간대를 검증하고 불완전 구간을 표시해야 함.
3. 비용: 동일 비교 기간·생산 조건의 기준 운영 비용과 계획 운영 비용, 전력/연료 단가, 부품·정비·정지 비용의 근거. 단순 차이는 인과적인 절감 성과를 증명하지 않음.
4. 마모/RUL: 설비·롤·위치 매핑, 실측 기준, 계산 모델 버전, 누적 운전시간, 예측 불확실성, 교체일 산정에 사용한 향후 운전 일정.
5. 이벤트 상관: 이벤트 시각·종류, 전후 분석 구간, 생산량/마모 변수 정의, 표본 수, 분석 방법, 교란 요인. 그래프의 동시 변화만으로 기여도나 원인을 단정하지 않음.

전류 A만으로 소비전력·전력량·분쇄 원단위(kWh/t)를 산출하지 않는다. 필요한 계측 또는 전압·역률 등 검증된 전기 파라미터가 있어야 한다. 새 화면의 해당 값은 자료가 없으면 미산정이다.
