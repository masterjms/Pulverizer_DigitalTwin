# B 실측 기록 로컬 연결 — 2026-09-28

## 확인 결과

찾은 Docker 컨테이너 `pulverizer-timescaledb`의 `pulverizer_db.public.sensor_readings`는 지정 엑셀과 **동일하지 않다**.

| 항목 | 기존 Docker DB | 지정 엑셀 |
|---|---|---|
| 설비 | PV-001 | 미분기 B |
| 행 수 | 101,181 | 864,000 |
| 첫 시각 | 2026-07-12 19:33:04.037234 UTC | 2025-12-10 00:00:00 KST 해석 |
| 마지막 시각 | 2026-07-21 06:17:40.851844 UTC | 2025-12-19 23:59:59 KST 해석 |
| 열 | bowl_rpm, motor_current, bearing_temp, vibration, tire_wear 등 | 원본 14개 DCS 태그 |
| 기록 기간 교집합 | 엑셀 기간에 해당하는 DB 행 0건 | — |

기존 DB에는 alarm_events 1,609건도 있다. Unity/Backend/main.py의 `create_pulverizer_data`는 PV-001 모의 센서를 생성하며 motor_current는 23.4±3.0 범위이다. DB의 해당 값 범위 20.4~26.4와 부합한다. 이 DB의 마모·경보를 B 실측으로 사용하지 않았다. 원격 서버 DB는 조회하지 않았다.

원본 컨테이너는 정지 상태이며 포트 5432는 다른 프로젝트가 사용 중이었다. 기존 `backend_timescale_data` 볼륨을 같은 이미지의 임시 컨테이너로 열었다. 외부 네트워크/포트 없이 Unix socket으로만 조회하고 SQL 읽기 전용, autovacuum 및 Timescale 작업자 비활성화 상태로 확인했다. 조회 후 임시 컨테이너는 정상 종료·제거했고 볼륨과 원본 컨테이너는 보존했다. 기존 테이블을 덮어쓰거나 다른 프로젝트를 중지하지 않았다.

## 엑셀 전체 검증

- 파일: `C:/Users/mstot/Downloads/미분기 B (12.10~12.20).xlsx`
- 시트/범위: `Data Normalize!D19:R864018` (D=시각, E:R=14개 태그)
- 실제 기간은 12월 10일~19일이다. 파일명 12.20의 자정 행은 없다.
- 864,000행, 숫자 셀 12,096,000개. 863,999개 인접 간격 모두 정확히 1초. 중복/역순/누락 없음.
- 모터 상태는 모든 행에서 1. 따라서 이 기간 재생 중 가동/정지 전환이 없는 것이 정상이다.
- SQLite 재조회 전체 행을 원본 순서로 비교한 SHA-256 지문이 일치한다. 숫자는 반올림 없이 binary64로 저장했다.
- 엑셀 SHA-256: `05df053af786aeb0c20df7bd5edebadb1336bf8a0cbfd78d7b7da3ffbe495260`
- 시각+14개 값 지문(원본/저장 후 동일): `984cc2904843ef0344b29110593376b2f84b7f6964b12a09af9e0a63c79d9873`
- 자세한 태그별 결측/범위/단위: `Data/local-b/verification.json`
- 엑셀 시각 자체에는 시간대가 없다. 발전소 현지 시간인 KST(+09:00)로 해석했고 원래 벽시계 시각을 바꾸지 않았다.

## 실제 연결 구조

엑셀 → 전체 검증 → `Data/local-b/history.sqlite` → 읽기 전용 Python API(127.0.0.1:8143) → Vite 동일 출처 프록시 → React / Unity 설비 상태.

기존 Docker 데이터와 엑셀이 다르므로 별도 SQLite 저장소를 사용한다. 새 저장소는 Docker DB가 아니다. 원본 1초 데이터 전체를 보존하며 `/Data/`는 Git에서 제외된다. 민감한 원본은 배포 zip/public 폴더에 넣지 않는다.

실행:

```powershell
& 'C:\Users\mstot\PulverizerTwin\Tools\Start-Ce883Local.ps1'
```

화면: `http://127.0.0.1:5173/react-twin/?viewer=ce883`

1. **로컬 B 연결** → B 절개 화면, 과거 가동 상태 표시.
2. 재생/타임바 → 5분 간격 실제 측정점을 재생 (평균이나 보간 아님). 마지막 23:59:59도 포함한 2,881개 점.
3. **B 원본 운전 기록 → 원본 시각 → 해당 초 조회** → 임의의 1초와 14개 원값 조회.
4. 분해 → 검증된 운전 변수 표시. 마모 측정값은 없어 30위치는 회색/—.
5. 외형 → B만 해당 과거 시점의 가동 상태. A/C/D/E/F는 확인 불가.

소스 재가져오기가 필요하면 `Tools/local_b_data.py --import-xlsx <경로>`를 사용한다. 기존 검증 캐시가 있으면 덮어쓰지 않고 중단한다. 현재 파일은 이미 가져왔으므로 다시 실행할 필요 없다.

API(모두 GET, 기존 DB에 접근하지 않음):

- `/api/local-b/health`: 서비스 식별/읽기 전용
- `/api/local-b/audit`: 원본 파일 지문/전체 검증 메타데이터
- `/api/local-b/workspace`: B v2 화면 데이터
- `/api/local-b/point?at=<시간대 포함 시각>`: 정확히 해당 초의 14개 원값. 존재하지 않으면 404.

## 해석 제한

- MOTOR POWER의 엑셀 단위 `CU.M/H`와 기존 정의서 `A`가 충돌한다. 원값은 보여주지만 전류/전력/비용으로 변환하지 않는다.
- 분급기 속도 원본의 단위 칸이 공란이다. 원값을 보여주고 RPM은 미확인 처리한다. 실측되지 않은 보울 RPM/회전 애니메이션도 만들지 않는다.
- MW는 3호기 공통 순간 출력이다. B의 생산량이나 A~F 합산 출력으로 취급하지 않는다.
- 마모 실측, 초기/교체 시각, 정비 내역, 비용 단가, 발전 목표가 없으므로 마모/RUL/교체일/목표 달성률/비용은 산정하지 않는다. 20,000h는 관행 비교 기준일 뿐이다.
- 밸브 신호를 임의의 고장/정비 이벤트로 바꾸지 않았다.

## 검증 명령

```powershell
# 번들 Python 또는 openpyxl이 있는 Python
python C:\Users\mstot\PulverizerTwin\Tests\test_local_b_data.py
# 프론트 폴더, Node 24
node --test tests/ce883-contract.test.ts tests/ce883-depth.test.ts tests/ce883-local-b.integration.test.ts
npm run build
npx eslint src/ce883 vite.config.ts
```

전체 캐시 읽기 전용, 원본 첫 행 수치, 5분 표본/마지막 초, 단일 셀 변경 감지, 브라우저 계약, 1초 API, 기간 밖 404를 검증한다. 기존 계약 16개 + B API 통합 3개 + Python 4개 통과.
