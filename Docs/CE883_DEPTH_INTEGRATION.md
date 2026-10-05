# 3단계 디지털 트윈 연결 — v2

## 화면과 상태 흐름

React 진입점은 `C:/Users/mstot/pulverizer-frontend/src/ce883/DepthWorkspace.tsx`다. `?viewer=ce883`에서 실행되며 기존 기본 페이지와 분리된다. 이전 `InspectionPage.tsx`는 보관된 단일 모델 검토 화면이다.

- 외형: 공유 메시로 구성한 A~F 외형 6대, 설비별 상태등, 발전 목표와 비용 비교. 실제 공장 배치도가 아닌 탐색용 2×3 배치다.
- 설비 클릭 → 절개: 선택 설비와 교체 판단, 관행 20,000 h 비교, 기록 재생, 정비 이력.
- 롤 접촉면 클릭 → 분해 단계의 부품 확대: 3롤×10위치, 마모 추이와 이벤트, 운전 변수, 정비 이력. `분해 전체로`는 전체 분해 모델을 보여준다.
- 상단 단계 버튼은 모든 사용자에게 열린다. 계정 역할에 고정하지 않는다. 설비·단계 이동 시 기준 시점을 유지한다.

## 데이터 수신

`기록 불러오기`는 JSON 파일을, `데이터 연결`은 동일 출처 읽기 API를 받는다. 외부 서버에는 직접 연결하지 않는다. API 입력란의 `/api/v2/twin/workspace`는 경로 예시이며 이번 작업에서 해당 운영 API를 배포한 것은 아니다. 기존 마모 v1 단일 프레임도 수용하지만 없는 운전·교체 값은 null로 남긴다.

정확한 타입과 검증의 기준: `src/ce883/depthContract.ts`. 실행 가능한 전체 예제: `public/ce883-data/TEST_ONLY_depth_example.json`. 이 예제는 UI 검증용으로 생성했으며 실제 기록이 아니다.

최상위 v2 구조:

```json
{
  "schemaVersion": 2,
  "title": "검증된 기록의 이름",
  "generatedAt": "2026-09-28T09:00:00+09:00",
  "scenario": false,
  "baselineHours": 20000,
  "machines": [],
  "production": null,
  "costs": null
}
```

`machines` 각 항목: machineId(`pulverizer_a`~`pulverizer_f`), maxGapSeconds(기록 유효 간격), snapshots, events, maintenance, correlation(null 허용).

`snapshots`는 중복 없이 시각 오름차순이다. 각 항목:

- at: 시간대가 포함된 ISO 시각.
- source: measured / model / forecast / scenario / experiment. scenario 데이터는 최상위 scenario:true가 필요하다.
- status: running / stopped / fault / unknown. 원본 MTR_STATUS 1→running, 4→stopped, 다른 코드·결측을 가동으로 추정하지 않는다.
- wear: 기존 WearFrame v1 또는 null. 설비·시각·출처가 상위 스냅샷과 일치해야 한다.
- operating: plantMw, motorCurrentA, feedTph, amwi, journalSetPsi, journalActualPsi, bowlDpMmH2o, classifierRpm, specificEnergyKwhPerTon. 단위는 이름에 명시했으며 결측은 null.
- reliability: wearMeanMm, wearRateUmPerHour, remainingHours, replacementAt, cycleOperatingHours, basis. 프론트는 서버 계산 결과를 표시한다. 달력일과 운전시간을 나누어 보여준다.

이벤트는 id, at, title, kind(operation/alarm/maintenance/replacement), source, partId, zone, wearMm, beforeRateUmPerHour, afterRateUmPerHour, note를 받는다. partId는 tire_01~03 또는 null, zone은 1~10 또는 null이다. 전후 마모율이 없으면 임의 계산하지 않는다.

정비 이력은 id, at, component, action, note, source. 상관 분석은 coefficient(-1~1), sampleCount(3 이상), method, period, source, note를 갖는 검증된 결과 또는 null이다. 표본 수 3은 형식 검증의 최소값일 뿐 통계적 유의성을 보장하지 않는다.

production: date(KST 운영일), asOf, targetMwh(양수), actualMwh, source, basis. 달성률은 actual/target이며 100% 초과를 유지한다. 현재 커서보다 나중의 집계 또는 다른 운영일 집계를 표시하지 않는다.

costs: period, asOf, baselineKrw, plannedKrw, source, basis. baseline-planned가 음수면 비용 증가로 표시한다. 현재 커서보다 나중 집계는 표시하지 않는다. 이 필드는 단일 기간의 집계이며 여러 운영일·계획 버전 탐색은 서버 조회 확장으로 연결해야 한다.

## 재생과 결측 처리

시각 커서 이전의 마지막 스냅샷만 사용한다. maxGapSeconds보다 오래됐거나 이전 기록이 없으면 미확인으로 돌린다. B의 마모를 다른 설비로 복제하지 않는다. 미래 정비 기록은 이력 목록에서 숨긴다. 전체 기간의 이벤트 그래프는 탐색을 위해 표시하며 커서선을 별도로 보여준다.

타임바는 저장된 상태를 재생하는 기능이다. 운전 변수를 바꾸어 미래 마모를 재계산하는 시뮬레이터는 이번 화면에 구현하지 않았다. `forecast` 마모 구간은 점선으로 구분한다. y축은 누적 마모(mm)이며 잔여 두께가 아니다.

파일/API 응답은 최대 5 MB, 설비별 스냅샷 6,000건·이벤트 3,000건이다. 수년 원시 데이터를 한 번에 브라우저로 보내지 말고 서버에서 기간별 집계·조회해야 한다. 실시간 자동 폴링·인증·운영 서버 어댑터는 후속 연결 범위다.

## Unity 오브젝트와 이벤트

상세 프리팹: `Assets/NextroTwin/Prefabs/CE883HP.prefab`. Runtime/TwinFleet.cs가 외형 메시를 재사용해 실행 시 `FleetOverview/Mill_A`~`Mill_F`를 생성한다. 편집 씬에는 상세 한 대가 보이고 Play/WebGL에서는 6대 외형으로 시작한다.

`NextroTwinBridge` 수신: FocusMachine, SetView, FocusComponent, ApplyWearState, ClearData, SetTimeCursor, ApplyFleetState, SetCamera, PickViewport. ApplyFleetState는 6개의 고유 설비 ID와 status를 모두 받는다. React는 깊이 변경 시에도 마모 프레임을 재전송해 모델 선택에 따른 데이터 초기화를 복구한다.

브라우저 `nextro-twin` 이벤트: ready, machinePicked, partSelected, selectionCleared, error. C# 파트 ID 및 30개 마모 메시 구조는 기존 통합 문서를 따른다. 외형 표시에는 최소한의 공유 메시를 쓰고, 상세 단계는 기존 세부 메시와 개별 마모 머티리얼을 사용한다.

## 운영 연결 전 검증

원본 단위와 누락 데이터 조사 결과는 `CE883_DATA_AUDIT.md` 참조. 실제 A~F 시계열 및 마모 모델의 검증된 출력을 위 DTO로 매핑해야 한다. 로컬 데모 상수나 출처 불명의 wear_sim 출력물을 실측으로 바꾸어 넣지 않는다.

웹 빌드 배포 시 Publish-Ce883Local.ps1이 네 개 Unity 리소스의 SHA-256으로 build-version.json을 생성한다. React는 버전 파일을 캐시 없이 읽고 같은 버전 쿼리를 모든 Unity 리소스에 적용한다. 빌드 후에는 이 스크립트로 다시 복사해야 한다. wear.running은 status가 running인 경우에만 true여야 하며, UI·Unity 상태가 충돌하면 입력을 거부한다.
