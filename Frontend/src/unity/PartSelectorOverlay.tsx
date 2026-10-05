import { useTwinStore } from '../store/twinStore';

// ovzexx/CoalPulverizer3D 레퍼런스의 좌상단 플로팅 부품 선택 버튼.
// "롤 타이어"만 실제 백엔드 데이터가 있어 동작한다 - 나머지는 시각적 자리만
// 맞춰두고 클릭해도 아무 일이 없다 (향후 그라인딩볼/분리기 데이터 확보 시 연결).
export function PartSelectorOverlay() {
  const mode = useTwinStore((state) => state.mode);
  const selectTire = useTwinStore((state) => state.selectTire);
  const clearSelection = useTwinStore((state) => state.clearSelection);

  const isTireSelected = mode === 'tire-detail';

  return (
    <div className="absolute left-3 top-3 z-[5] flex flex-col gap-1.5">
      <div className="mb-0.5 text-[10px] text-[#8b949e]">부품 선택</div>
      <button
        type="button"
        onClick={() => selectTire('roll_tire')}
        className={`rounded-md border px-3.5 py-1.5 text-xs backdrop-blur-sm transition-colors ${
          isTireSelected
            ? 'border-[#2dcfae] bg-[#2dcfae]/10 text-[#2dcfae]'
            : 'border-[#30363d] bg-[#161b22]/90 text-[#e6edf3] hover:border-[#2dcfae] hover:text-[#2dcfae]'
        }`}
      >
        🔩 롤 타이어
      </button>
      <button
        type="button"
        disabled
        title="아직 데이터 연동 전입니다"
        className="cursor-not-allowed rounded-md border border-[#30363d] bg-[#161b22]/60 px-3.5 py-1.5 text-xs text-[#8b949e]/50 backdrop-blur-sm"
      >
        ⚙ 그라인딩 볼
      </button>
      <button
        type="button"
        disabled
        title="아직 데이터 연동 전입니다"
        className="cursor-not-allowed rounded-md border border-[#30363d] bg-[#161b22]/60 px-3.5 py-1.5 text-xs text-[#8b949e]/50 backdrop-blur-sm"
      >
        🌀 분리기
      </button>
      {isTireSelected && (
        <button
          type="button"
          onClick={clearSelection}
          className="rounded-md border border-[#30363d] bg-[#161b22]/90 px-3.5 py-1.5 text-xs text-[#e6edf3] backdrop-blur-sm transition-colors hover:border-[#ef4444] hover:text-[#ef4444]"
        >
          ✕ 닫기
        </button>
      )}
    </div>
  );
}
