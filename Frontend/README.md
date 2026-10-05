# pulverizer-frontend

미분기(Coal Pulverizer) 디지털 트윈 — React 프론트엔드.

레퍼런스: `cadlab312server-1.tail43a79b.ts.net/app`의 **"Unity 3D 프로토타입"** 탭에
최종적으로 이식될 화면. 자세한 배경/아키텍처/로드맵은 바탕화면의
`미분기_디지털트윈_통합계획.md` 참고.

**디자인/UI 출처**: [github.com/ovzexx/CoalPulverizer3D](https://github.com/ovzexx/CoalPulverizer3D)
(`docs/index.html` - 하드코딩 HTML/CSS/JS 데모)의 레이아웃·색상·인터랙션을 그대로
이식했다. 색상 팔레트는 `src/theme.ts`에 모아뒀다. 그쪽은 데이터가 하드코딩된 데모이고,
여기는 전부 우리 FastAPI 백엔드에서 실데이터를 받아온다는 점이 다르다.

## 지금 상태

`src/unity/UnityWebglEmbed.tsx`가 `PulverizerTwin`에서 빌드한 실제 Unity WebGL을
로드한다. 빌드 산출물(`public/unity-build/`)은 ~60MB라 git에 커밋하지 않으므로
(`.gitignore` 처리됨), 새로 클론했거나 Unity 쪽 모델이 바뀌면 다시 빌드해서
복사해야 한다:

```bash
# PulverizerTwin 프로젝트에서 (Unity Editor 메뉴 또는 batchmode):
#   Tools > Pulverizer Twin > Build WebGL
# 산출물은 PulverizerTwin/Builds/WebGL/Build/ 에 생성됨

cp -r "../PulverizerTwin/Builds/WebGL/Build" "public/unity-build/"
```

빌드 파일명(`WebGL.loader.js` 등)은 `UnityWebglEmbed.tsx`의 `BUILD_NAME` 상수와
일치해야 한다 — 프로덕트 이름이 아니라 Unity가 자동으로 붙이는 이름이므로,
빌드 후 `public/unity-build/Build/` 안의 실제 파일명을 확인할 것.

Unity 최종 모델이 아직 없거나 WebGL 빌드 없이 프론트만 빠르게 테스트하고 싶으면
`src/unity/PlaceholderTwinViewer.tsx`(SVG로 만든 대체 뷰어, 실제 Unity와 동일한
`emitUnityBridgeEvent`를 발생시킴)로 `App.tsx`의 `<UnityWebglEmbed />`를 바꿔
끼우면 된다 — `bridge.ts`의 계약은 두 구현 모두 동일하게 따른다.

**참고**: Unity WebGL 캔버스는 반드시 `id` 속성이 있어야 한다 — 없으면 Unity
프레임워크가 키보드 이벤트 리스너를 붙일 때 빈 CSS selector(`#`)를 만들어
`SyntaxError`가 난다 (`UnityWebglEmbed.tsx`에서 이미 처리됨).

## 아키텍처

```
Unity WebGL(또는 placeholder) ←→ bridge.ts ←→ Zustand(twinStore) ←→ React UI
                                                      │
                                          overview / tire-detail
                                             레이아웃 자동 전환
                                                      │
                                              REST API (FastAPI)
```

- **상태관리**: `src/store/twinStore.ts` (Zustand) — `mode`(overview/tire-detail),
  `selectedTireId`, `dataMode`(live/scenario), `scenario`(접촉폭 시나리오 등)
- **Unity 브리지**: `src/unity/bridge.ts` — Unity ↔ React 이벤트 계약
- **API 클라이언트**: `src/api/client.ts`, 타입은 `src/types/api.ts`
  (Backend/main.py, wear_engine.py 응답 스키마와 1:1 대응. 필드 변경 시 같이 갱신)

## 개발 환경 설정

```bash
npm install
cp .env.example .env   # 필요시 API 주소 수정
npm run dev
```

Backend(FastAPI)가 `http://127.0.0.1:8000`에서 실행 중이어야 한다
(`PulverizerTwin/Backend` — `python run.py`, TimescaleDB는 `docker compose up -d`).

## 주의사항

- 계산(마모식 등)은 절대 여기서 하지 않는다 — FastAPI 응답을 그대로 표시만 한다.
- API 응답 필드가 바뀌면 `src/types/api.ts`도 같이 갱신할 것. 추후 안정화되면
  `openapi-typescript`로 자동 생성 전환을 고려한다.
