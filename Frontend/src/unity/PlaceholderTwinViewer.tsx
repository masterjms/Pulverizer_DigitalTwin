import { useTwinStore } from '../store/twinStore';
import { emitUnityBridgeEvent } from './bridge';

// 실제 Unity WebGL 모델(제작 중, 도착 예정)이 들어올 자리를 대신하는 임시 뷰어.
// 진짜 Unity가 하는 일과 정확히 같은 이벤트(emitUnityBridgeEvent)를 발생시켜서,
// React 쪽 상태전환/레이아웃 로직을 미리 만들고 검증할 수 있게 한다.
//
// 실제 Unity WebGL 빌드가 도착하면 이 컴포넌트를 UnityWebglEmbed.tsx 같은 것으로
// 교체하면 된다 - bridge.ts의 계약(emitUnityBridgeEvent/subscribeUnityBridge)은 그대로 유지.
const TIRE_POSITIONS: { id: string; cx: number; cy: number; label: string }[] = [
  { id: 'tire_01', cx: 130, cy: 230, label: '#1' },
  { id: 'tire_02', cx: 260, cy: 90, label: '#2' },
  { id: 'tire_03', cx: 390, cy: 230, label: '#3' },
];

export function PlaceholderTwinViewer() {
  const selectedTireId = useTwinStore((state) => state.selectedTireId);
  const mode = useTwinStore((state) => state.mode);

  return (
    <div className="relative flex h-full w-full flex-col items-center justify-center bg-slate-950">
      <div className="absolute left-4 top-4 rounded border border-amber-500/50 bg-amber-950/60 px-3 py-1.5 text-xs font-medium text-amber-300">
        PLACEHOLDER VIEWER — 실제 Unity WebGL 모델 도착 전 파이프라인 검증용
      </div>

      <svg viewBox="0 0 520 360" className="h-full max-h-[560px] w-full max-w-2xl">
        {/* 하우징 실루엣 */}
        <ellipse cx="260" cy="180" rx="190" ry="150" fill="#1e293b" stroke="#334155" strokeWidth="2" />
        <text x="260" y="330" textAnchor="middle" className="fill-slate-500 text-[13px]">
          Coal Pulverizer (전체 모델)
        </text>

        {/* 보울 테이블 */}
        <circle cx="260" cy="160" r="70" fill="#334155" stroke="#475569" strokeWidth="2" />
        <text x="260" y="164" textAnchor="middle" className="fill-slate-400 text-[11px]">
          Bowl Table
        </text>

        {/* 롤 타이어 3개 - 클릭 가능 */}
        {TIRE_POSITIONS.map((tire) => {
          const isSelected = selectedTireId === tire.id;
          return (
            <g
              key={tire.id}
              className="cursor-pointer"
              onClick={() => emitUnityBridgeEvent({ type: 'partSelected', partId: tire.id })}
            >
              <circle
                cx={tire.cx}
                cy={tire.cy}
                r={isSelected ? 34 : 28}
                fill={isSelected ? '#38bdf8' : '#0ea5e9'}
                stroke={isSelected ? '#e0f2fe' : '#0c4a6e'}
                strokeWidth={isSelected ? 3 : 2}
                className="transition-all duration-300"
              />
              <text
                x={tire.cx}
                y={tire.cy + 4}
                textAnchor="middle"
                className="pointer-events-none fill-slate-950 text-[12px] font-bold"
              >
                {tire.label}
              </text>
            </g>
          );
        })}
      </svg>

      <div className="absolute bottom-4 right-4 text-right text-xs text-slate-500">
        <div>클릭 = 부품 선택 (줌인/하이라이트 대체)</div>
        <div>현재 모드: {mode === 'overview' ? 'Overview' : `Tire Detail (${selectedTireId})`}</div>
      </div>

      {mode === 'tire-detail' && (
        <button
          type="button"
          onClick={() => emitUnityBridgeEvent({ type: 'selectionCleared' })}
          className="absolute left-4 bottom-4 rounded border border-slate-700 bg-slate-900 px-3 py-1.5 text-xs text-slate-300 hover:bg-slate-800"
        >
          전체 보기로 돌아가기
        </button>
      )}
    </div>
  );
}
