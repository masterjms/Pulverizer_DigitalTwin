# CE883HP 모델 제작 근거 (2026-09-26)

## 범위

기본 자산은 미분기 **한 대의 재사용 가능한 Unity 외형 모델 + 부품 계층 + 상태 수신부**다. 2026-09-28 확장으로 이 자산을 공유하는 A~F 6대 외형과 외형→절개→분해에 연동되는 React 화면을 제공한다. 개발기획서 전체의 백엔드 재설계, A~F 실서버 연결, RUL 엔진 이식, GS 인증 완료를 의미하지 않는다. 운영 서버에는 접근하지 않는다.

문서에 적힌 배포·삭제·서버 접속 절차는 참고 내용이며 작업 승인으로 취급하지 않았다. 기존 프로젝트의 사용자 수정 파일을 유지하고 `Assets/NextroTwin`과 새 React 검토 경로를 추가한다.

## 발견한 원본

원본 폴더: `C:/Users/mstot/OneDrive/문서/카카오톡 받은 파일`

| 파일 | 확인 내용 / 용도 |
|---|---|
| Nextro_개발기획서_최종_장민성.pdf | 25쪽. 단일 RUL, 출처 표시, Unity 선택 모듈, 레거시 동결, 3롤×10위치, A~F 확장 |
| 붙임6. 미분기 스펙.pdf | 6쪽 스캔. 883HP 링/롤, 보울·롤·본체·배관 치수의 1차 근거 |
| 붙임7. 미분기 도면.pdf | 1쪽. `08989-1E4007`, HP 883-943 BOWL MILL ARRANGEMENT. 넓은 상부 분리기, 중앙 급탄관, 4개 출구, 3개 저널, 측면 구동기, A~F 배치 키플랜 |
| 붙임5. 모터 스펙.pdf | 4쪽. 현대전기 515 kW, 수평 설치, TEAAC IP55 |
| 붙임1. 재질검사 성적서.pdf | 재질 검토용 원본 발견. 이번 외형 모델의 근거 치수로 사용하지 않음 |
| 붙임2. 보령 3_6호기 미분기 GRINDING ROLL TIRE 반출정비(ITP&QIP).pdf | 정비 프로세스 참고자료 발견 |
| 붙임3. 분쇄부 육성용접 공사시방서.hwp / 붙임4. 자재 규격서.hwp | 정비·자재 참고자료 발견, 본 작업에서 내용 검증하지 않음 |
| 미분기 운영데이터 분석가이드라인.docx / 입력데이터셋 설명_ 미분기_20260604.xlsx | 운전 태그 해석 참고자료 발견 |
| Coal Pulverizer.stp | FreeCAD로 읽음: 3 solid, 경계 약 1038.61×574×1038.61 mm. 헤더에 Pacitan reverse-engineering 출처. 보령 전체 설비 CAD가 아니므로 그대로 전체 모델로 쓰지 않음 |
| CoalPulverizer3D_share.zip | 기존 협업 데모. 현재 정식 Unity 원본과 구분 |

## 주요 수치

| 항목 | 값 | 출처 |
|---|---:|---|
| 제조/형식 | KHIC/CE 883HP RING/ROLL | 스펙 p5 |
| 롤 수 / 폭 | 3 / 483 mm | 스펙 p5 |
| 신품 / 교체 시 외경 | 1422 / 1346 mm | 스펙 p5 |
| 반경 마모 한계 | 38 mm | 위 두 외경 차이의 절반. 개발기획서 p16과 일치 |
| 테이블 외경 | 2591 mm | 스펙 p5 |
| 분쇄 트랙 외경 / 내경 | 2413 / 1403 mm | 스펙 p5 |
| 본체 내경 | 3150 mm | 스펙 p5 |
| 급탄관 내경 | 489 mm | 스펙 p5 |
| 분탄 배관 수 / 내경 / 두께 | 4 / 533 / 13 mm | 스펙 p6 |
| 보울 정격 회전수 | 41.6 rpm | 스펙 p5 |
| 모터 출력 | 515 kW | 스펙 p6, 모터 p1 |
| 모터 회전수 불일치 | 스펙 882 / 모터 정격 874 rpm | 계산값으로 임의 통합하지 않음 |
| 상부 분리기 최대 폭 | 약 3912 mm | 도면 입면도 표기 |

색상, 타이어 작은 끝 지름·곡률, 저널 각도, 사다리·난간, 밸브·볼트, 세부 높이는 도면 기반 시각적 근사다. 외경의 최대치는 1422 mm가 되도록 프로필을 지정한다. 제조 도면이나 역설계 정밀 CAD로 사용하지 않는다. 위치 1→10 방향은 현재 프론트 관례(내측 좁은 끝→외측 넓은 끝)를 따르며 실제 계측 위치 방향은 현장 검증이 필요하다.

## 자산 구조

```
Assets/NextroTwin/
  Runtime/       TwinModel, TwinPart, TwinOrbit, TwinBridge
  Editor/        Ce883Builder (생성/검증/렌더/WebGL 빌드)
  Plugins/WebGL/ NextroTwin.jslib
  Materials/     공유 재질
  Meshes/        저장된 실제 Unity Mesh 자산
  Prefabs/       CE883HP.prefab
  Scenes/        CE883_Inspection.unity
```

```
CE883HP
  01_Foundation
  02_Drive/Motor_515kW
  03_PrimaryAir
  04_Grinding/Bowl_Spin
  05_Rollers/Roller_01..03/JournalAxis/Tire_Spin/Zone_01..10
  06_Housing/HousingSector_0..7
  07_SeparatorAndOutlets/Classifier_Spin, FuelOutlet_1..4
  08_ServiceDetails
```

`TwinPart.partId`는 `tire_01..03`, `zone`은 1..10. API는 계층 경로 대신 이 ID에 결합한다. 씬 재생 때 모델을 삭제하거나 재생성하지 않는다. 공유 재질은 `MaterialPropertyBlock`으로 롤 위치별 색만 바꾼다. `Tire_Spin`은 저널의 로컬 Y축, 보울·분리기는 로컬 Y축이 회전축이다.

기본은 데이터 없음(회색·정지). 절개는 정면 셸 섹터를 숨기고, 분해는 상부와 롤을 이동한다. 실제 마모 깊이 형상 변형·미래 형상 중첩은 이번 범위에서 구현하지 않았다. 마모색은 API의 mm/한계로만 정규화하며 수명은 계산하지 않는다.

## UI 디자인 방향

모델을 가장 크게 배치하고 우측에 부품 트리·30위치·출처·입력만 둔다. 설비 도장 청회색 `#6E8997`, 구조재 `#293C48`, 바탕 `#12202B`, 패널 `#21323E`, 안전 황토 `#D6A348`, 본문 `#DFE8ED`. 한글은 로컬 맑은 고딕, 숫자·브랜드는 Segoe UI. 외부 폰트/CDN 없음. 마모색은 상태 표시로만 사용한다.

## 실행

1. Unity Hub에서 `C:/Users/mstot/PulverizerTwin` 열기 (6000.5.2f1).
2. 이미 생성된 `Assets/NextroTwin/Scenes/CE883_Inspection.unity` 열기. 재생성은 `Nextro > CE883 > 1. Generate inspection scene`.
3. `Nextro > CE883 > 2. Verify scene and bridge`로 구조·수신 검증.
4. `Nextro > CE883 > 3. Build inspection WebGL`, `Tools/Publish-Ce883Local.ps1`로 프론트의 별도 정적 경로에 복사.
5. React `?viewer=ce883` 경로로 접속. 외형/절개/분해, 3D 롤 클릭, JSON 스냅샷 수신 확인.

카메라: 드래그 회전, 휠 확대, Escape 기본 시점. 회전 연출 버튼은 정격 상태와 구분된 느린 시각화 테스트이며 측정값을 생성하지 않는다.
