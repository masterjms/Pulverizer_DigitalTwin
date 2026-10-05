# Nextro CE883HP

Unity 6000.5.2f1 / Universal Render Pipeline / Input System.

- 씬: `Scenes/CE883_Inspection.unity`
- 프리팹: `Prefabs/CE883HP.prefab`
- 메뉴: `Nextro > CE883` (생성, 검증, WebGL 빌드)
- 3개 롤, 각 10개 선택·마모 영역. 단위 m. 주요 설비 치수는 원본 사양 기준, 세부 형상은 시각적 근사.
- 씬 수신 오브젝트: `NextroTwinBridge`, 메서드 `ApplyWearState`.
- 기본 상태는 데이터 없음. 계산은 백엔드에서 수행한다.
- 상세 사양·제작 근거·메시지 JSON: 프로젝트 `Docs/CE883_SOURCE_AND_MODEL.md`, `Docs/CE883_INTEGRATION.md`.
- 시험용 색 분포는 실측이 아니다. 운영 데이터에 대한 검증·보정·잔여수명 계산은 별도 백엔드 작업이다.

기존 Assets/Scripts, 기존 씬·프리팹과 분리되어 있다. 이 폴더의 런타임은 기존 레거시 스크립트에 의존하지 않는다.
