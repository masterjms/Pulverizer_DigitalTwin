# CE883 계약 검증

Node 24 기준:

```
node --test tests/ce883-contract.test.ts
npm run build
npx eslint src/ce883 src/App.tsx
```

브라우저: `/react-twin/?viewer=ce883`. Unity 빌드 후 외형/절개/분해, 롤 클릭→30위치 표 동기, 위치 선택→강조, 회전 시작/정지, 설비 A~F 전환을 확인한다.

테스트용 JSON은 `C:/Users/mstot/PulverizerTwin/Tests/fixtures/TEST_ONLY_scenario_B.json`.
실제 운전·마모 데이터가 아니다. 수동 파일 입력으로만 사용하고 운영 API 폴백 또는 public 기본 데이터로 배포하지 않는다.

- B 선택 + B 시나리오: 출처 시나리오, 30위치 색 적용.
- B 선택 + A 시나리오: 설비 불일치 오류, 이전 정상값 유지.
- A 전환: 회색·데이터 없음, B 값 지워짐.
- B 재선택: 재입력 전까지 회색·데이터 없음.
- 파일 형식 오류 / 1 MB 초과: 오류 안내.
- Unity 로드 실패: 입력/2D 위치 격자 유지. 가짜 실측·RUL 없음.
