# Nextro · Pulverizer Digital Twin

CE 883HP 미분기의 Unity 모델과 React 화면, 로컬 B 운전 기록 조회 도구를 함께 관리합니다.

![분해 단계 PC 화면 — 가상 데이터 예제](Evidence/ce883/field-desktop-1920.png)

## 현재 구현

- **외형:** 미분기 A~F 전체 상태 확인.
- **절개:** 선택 설비의 부품·교체 판단 정보.
- **분해:** 롤 하나 확대, 위치 1~10 마모 표시 계약, 타임바, 잔여수명·이벤트 그래프, 독립 스크롤 상세 패널.
- 이벤트 선택과 Unity 부품/시간 커서/운전 변수 연동.
- 로컬 B 운전 기록의 읽기 전용 API 및 원본 Excel→SQLite 검증 도구.
- 실데이터와 가상 UI 예제를 명확히 구분.

**현재 실제 B 마모·잔여수명 예측은 연결 전입니다.** 미래 시뮬레이션 영역은 검토용 자리이며 계산 기능은 구현하지 않았습니다. A·C·D·E·F의 실측 운전 데이터는 이 저장소에 포함되어 있지 않습니다. `wear_sim` 기존 연구 코드는 별도 프로젝트 `pulverizer-lab`에 있으며 이번 저장소에 통합하지 않았습니다.

## 폴더

| 경로 | 내용 |
|---|---|
| `Assets/NextroTwin/` | CE883 모델·프리팹·씬·C# 브리지·생성/검증 도구 |
| `Packages/`, `ProjectSettings/` | Unity 프로젝트 설정 |
| `Frontend/` | React + TypeScript + Vite, CE883 UI 및 이전 UI |
| `Tools/local_b_data.py` | 로컬 Excel 수입·검증 및 읽기 전용 조회 서버(8143) |
| `Backend/` | 이전 FastAPI/TimescaleDB 프로토타입. CE883 기록 화면의 필수 서버가 아님 |
| `Docs/` | 설계·데이터 검토·연결 계약·검증 기록 |
| `Tests/`, `Frontend/tests/` | 데이터 처리·화면 계약 테스트 |
| `Evidence/ce883/` | 모델 및 화면 검증 이미지 |

## 빠르게 화면 실행하기 (Windows PowerShell)

필요: Git, Git LFS, Node.js **24 LTS**, npm. WebGL Release를 사용하면 Unity Editor 없이 화면을 확인할 수 있습니다.

```powershell
git clone https://github.com/masterjms/Pulverizer_DigitalTwin.git
cd Pulverizer_DigitalTwin
git lfs pull
cd Frontend
npm ci
cd ..
```

### WebGL 빌드 받기

[Releases](https://github.com/masterjms/Pulverizer_DigitalTwin/releases)에서 `CE883-WebGL-2026-10-05.zip`과 `SHA256SUMS.txt`를 받습니다. 저장소가 비공개이므로 로그인한 계정에 접근 권한이 필요합니다. GitHub CLI 사용 시:

```powershell
gh release download ce883-2026-10-05 --repo masterjms/Pulverizer_DigitalTwin --pattern "CE883-WebGL-2026-10-05.zip" --pattern "SHA256SUMS.txt" --dir .\Builds\downloads
Get-FileHash .\Builds\downloads\CE883-WebGL-2026-10-05.zip -Algorithm SHA256
Get-Content .\Builds\downloads\SHA256SUMS.txt
# 위 SHA256 값이 같은지 확인한 다음 압축 해제
Expand-Archive .\Builds\downloads\CE883-WebGL-2026-10-05.zip -DestinationPath .\Frontend\public -Force
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Start-Ce883Local.ps1
```

[로컬 화면](http://127.0.0.1:5173/react-twin/?viewer=ce883)을 열고 **가상 그래프 예제**로 UI를 확인합니다. 실제 운전 데이터가 없어도 이 예제는 동작합니다. 일반 `npm run dev`로 실행할 때도 URL에 `?viewer=ce883`를 넣어야 현재 화면이 열립니다.

## 실제 B 데이터

원본 엑셀·HWP·SQLite·DB 덤프·개인 환경변수는 업로드하지 않았습니다. 이미 검증한 캐시를 별도로 보유한 PC에서는 다음 위치로 복사합니다.

```text
Data/local-b/history.sqlite          # 2025-12-10~19
Data/march-b-review/history.sqlite   # 2026-03-20~31
```

기존 작업 PC의 원본 캐시는 `C:\Users\mstot\PulverizerTwin\Data\`에 있습니다. 이 저장소의 `Data/`는 Git에서 제외됩니다. 두 캐시를 배치한 후 시작 스크립트를 실행하면 B API도 실행합니다. 기존 5173/8143 서비스가 실행 중이면 새 프로세스를 중복 실행하지 않습니다.

12월 원본 Excel로 캐시를 새로 만들 때:

```powershell
python -m pip install -r Tools/requirements.txt
python Tools/local_b_data.py --import-xlsx "원본 Excel 경로"
```

위 단일 파일 수입 명령의 대상은 `Data/local-b/`입니다. 3월 자료를 이 명령으로 덮어쓰지 마세요. 3월 검증 캐시를 해당 별도 경로에 배치해야 합니다. 6개월 전체 파일을 자동 병합하는 도구는 아직 없습니다. 코드 20 원인·마모 측정 의미·교체 한계 등 미확정 사항은 데이터 검토 문서에 기록되어 있습니다.

## Unity에서 수정·재빌드

- Unity **6000.5.2f1**, Web Build Support, 활성화된 Editor 라이선스 필요.
- 이 저장소의 루트 폴더를 Unity Hub에서 엽니다.
- 씬: `Assets/NextroTwin/Scenes/CE883_Inspection.unity`.
- 메뉴 `Nextro → CE883 → 2. Verify scene and bridge`로 검증합니다.
- 메뉴 `Nextro → CE883 → 3. Build inspection WebGL`로 `Builds/CE883`에 빌드합니다.
- 루트에서 `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Publish-Ce883Local.ps1`을 실행하면 `Frontend/public/ce883-build`로 복사하고 캐시 버전 파일을 생성합니다.
- `1. Generate inspection scene`은 씬/생성 자산을 재생성하므로 수동 편집이 있다면 먼저 보관합니다.

## 검증

```powershell
cd Frontend
npm run build
node node_modules/eslint/bin/eslint.js src/ce883
node --test tests/ce883-contract.test.ts tests/ce883-depth.test.ts tests/ce883-event-life.test.ts
# 아래 통합 테스트는 실제 B 캐시 2개와 로컬 서버 실행이 필요
node --test tests/ce883-local-b.integration.test.ts
cd ..
python -m unittest discover -s Tests -v
```

마지막 Python 테스트도 기존 12월 B 캐시가 필요합니다. 현재 빌드에는 기존 번들 크기 권고 경고가 있습니다.

## 배포 파일과 이력

Git에는 편집 가능한 소스와 설정을 저장하고, WebGL은 Release에서 배포합니다. Unity의 Library/Logs/Builds, npm의 node_modules/dist는 재생성합니다. 모델·폰트·화면 이미지에는 기존 Unity Git LFS 규칙을 사용합니다. 원본 작업 폴더의 Git 이력·원격 설정은 변경하지 않고 이 저장소의 기존 이력 위에 통합했습니다.

상세 문서: [PC 배치](Docs/CE883_FIELD_DESKTOP_LAYOUT.md), [이벤트 UI](Docs/CE883_EVENT_LIFE_UI.md), [미래 시뮬레이션 제안](Docs/CE883_FUTURE_SIMULATION_PROPOSAL.md), [Unity 연동](Docs/CE883_DEPTH_INTEGRATION.md).

기존 문서에는 작업 당시의 로컬 경로와 중간 상태가 남아 있습니다. 현재 실행 경로와 구성은 이 README를 우선합니다. 포함된 타사 자산의 라이선스·출처 표기는 해당 파일과 Frontend README에 보존되어 있습니다.
