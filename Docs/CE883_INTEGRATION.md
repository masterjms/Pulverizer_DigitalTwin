# CE883 독립 뷰어 계약 v1

이 문서는 하위 호환을 유지하는 단일 마모 프레임 계약이다. 현재 6대 외형과 3단계 React 화면의 시계열·이벤트·정비·비용 계약은 `CE883_DEPTH_INTEGRATION.md`의 v2를 따른다. 사용 안내는 `CE883_DELIVERY.md`가 최신이다.

개발기획서의 설계 방향(계산/표시 분리, 준비 이벤트, 설비 ID, 10위치)을 구현하되 기존 미완성 브리지와 충돌하지 않도록 별도 `NextroTwinBridge` 오브젝트를 사용한다. 기존 `PartInteraction_Manager`, `Coal Pulverizer` 진입점은 수정하지 않았다.

기획서의 위치별 `zones01` 부분 갱신 대신 **설비 1대·롤 3개의 완전한 스냅샷**을 받는다. 누락 위치는 `valid:false`로 명시한다. mm 단위 원본과 백엔드의 한계를 보존하여 38/50 mm 혼용 및 부분 갱신 후 구값 잔존을 방지한다. 이는 기존 계약을 묵시적으로 대체하지 않는 별도 버전 어댑터다.

## React → Unity

`instance.SendMessage('NextroTwinBridge', method, stringValue)`

| 메서드 | 문자열 인수 | 동작 |
|---|---|---|
| FocusMachine | `{"machineId":"pulverizer_b"}` | A~F ID 검증, 구 데이터·회전 연출·부품 확대 초기화 |
| ApplyWearState | 아래 JSON | 검증 후 30개 위치 원자적 갱신 |
| FocusComponent | `{"machineId":"pulverizer_b","partId":"tire_01","zone":3}` | 해당 롤만 보이는 부품 확대·위치 강조. zone=0은 롤 전체 |
| SetView | `exterior / cutaway / exploded` | 외형/절개/분해 |
| SetCamera | `iso / front / side / top` | 카메라 프리셋 |
| SetTimeCursor | ISO 문자열 | 커서 메타데이터. 이 메서드만으로 과거 마모가 계산되거나 바뀌지 않음 |
| SetPreviewMotion | `1 / 0` | 테스트용 회전 연출, 운영 상태와 분리 |
| ClearData | 인수 없음 | 데이터 없음 색·정지 |
| ClearSelectionFromJS | 인수 없음 | 기본 카메라, 이벤트 에코 없음 |
| EnableBrowserPicking | `1 / 0` | React 포인터 이벤트 사용 여부. 켜면 Unity의 프레임 기반 클릭 판정을 끄고 중복 선택을 방지 |
| PickViewport | `{"x":0.5,"y":0.5}` | 캔버스 왼쪽 아래 기준 0~1 좌표로 실제 Collider를 선택하고 partSelected 반환 |

```json
{
  "schemaVersion": 1,
  "machineId": "pulverizer_b",
  "asOf": "2026-09-26T09:00:00+09:00",
  "source": "measured",
  "displayMode": "history",
  "running": false,
  "bowlRpm": 0,
  "classifierRpm": 0,
  "wearLimitMm": 38,
  "rollers": [
    {"componentId":"tire_01","wearMm":[0,0,0,0,0,0,0,0,0,0],"valid":[false,false,false,false,false,false,false,false,false,false]},
    {"componentId":"tire_02","wearMm":[0,0,0,0,0,0,0,0,0,0],"valid":[false,false,false,false,false,false,false,false,false,false]},
    {"componentId":"tire_03","wearMm":[0,0,0,0,0,0,0,0,0,0],"valid":[false,false,false,false,false,false,false,false,false,false]}
  ]
}
```

위 JSON은 **값 형식 설명이며 실측 샘플이 아니다**. `valid:false` 위치의 0은 전송 자리표시자이고 UI는 데이터 없음으로 표시한다. 0 mm 실측은 `valid:true, wearMm:0`과 명확히 구별된다. 한계를 초과한 마모도 수치 그대로 보존하며 빨강으로 표시한다. 색 경계는 0.35/0.60/0.80×한계다. RUL, 신뢰등급, 실측 앵커, 마모율은 Unity가 생성하지 않는다.

`source`: measured/model/forecast/scenario/experiment. `displayMode`: live/history/forecast/scenario. 날짜의 과거/미래는 화면 시간축 요청으로 정하며 타임스탬프의 대소만으로 과거 탐색을 거부하지 않는다. HTTP의 늦게 도착한 이전 설비 응답은 프론트의 요청 세대 번호로 버린다. 입력 오류는 기존 마지막 정상 스냅샷을 유지하고 오류를 표시한다.

## Unity → React

동일 페이지에서 `window.addEventListener('nextro-twin', handler)`. `event.detail`은 JSON 객체:

- `ready {buildVersion,machineId}`
- `machineSelected {machineId}`
- `partSelected {machineId,partId,zone}`
- `selectionCleared {machineId}` (Escape 키, JS 명령에는 에코하지 않음)
- `stateApplied {machineId}`
- `error {machineId,message}`

로더 Promise 완료와 Unity ready **둘 다** 확인한 뒤 초기 상태를 보내므로 초기 SendMessage 유실을 방지한다. 언마운트 시 Quit과 이벤트 리스너 정리를 수행한다. A~F는 동일 프리팹 재사용과 식별자 계약을 제공하며, 6대 실제 현장 배치 씬은 후속 작업이다.

React는 캔버스의 실제 화면 경계로 포인터 좌표를 정규화한다. 5 CSS px 이상 이동한 드래그는 선택으로 처리하지 않는다. 확대율·렌더 해상도에 따른 화면 좌표 차이와 프레임 사이의 짧은 클릭 누락을 줄인다. Unity Editor와 독립 WebGL 페이지는 기존 Input System 클릭 처리를 유지한다.

## 백엔드 연결 경계

현재 레거시 `/api/twin/predict`는 B 전용이며 한계 기본값 50 mm와 클라이언트 재계산 문제가 확인됐다. 이것을 A~F 공용 API인 것처럼 자동 연결하지 않는다. 새 조회 API는 백엔드에서 모델/실측의 **롤별 10위치 mm**를 확정한 뒤 위 DTO로 반환해야 한다. 이미 계산된 값을 전달하는 어댑터만 필요하며 Unity 측 수식 복사는 필요 없다. React 화면에서 같은 출처의 GET 경로를 지정하거나 내보낸 JSON을 불러올 수 있다. 백엔드 URL에 API 키를 넣지 않는다.

이 형식이 실측·모델 원천을 진실하게 표기하는지는 서버 어댑터 책임이다. 이 뷰어는 기존 Backend 합성 시뮬레이터를 자동 폴백으로 사용하지 않는다.

Unity 공식 API 참고: https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-interactingwithbrowserscripting.html
