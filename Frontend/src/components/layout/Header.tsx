import { useEffect, useState } from 'react';
import { useTwinStore } from '../../store/twinStore';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

// ovzexx/CoalPulverizer3D 레퍼런스의 헤더를 그대로 이식.
export function Header() {
  const [clock, setClock] = useState('');
  const dataMode = useTwinStore((state) => state.dataMode);
  const setDataMode = useTwinStore((state) => state.setDataMode);

  useEffect(() => {
    const tick = () => {
      const now = new Date();
      setClock(`${now.toLocaleDateString('ko-KR')} ${now.toLocaleTimeString('ko-KR')}`);
    };
    tick();
    const timer = setInterval(tick, 1000);
    return () => clearInterval(timer);
  }, []);

  return (
    <header className="flex h-[52px] shrink-0 items-center justify-between border-b border-[#30363d] bg-[#161b22] px-5">
      <div className="flex items-center gap-2.5">
        <svg width="28" height="28" viewBox="0 0 28 28" fill="none">
          <polygon points="14,2 26,8 26,20 14,26 2,20 2,8" fill="none" stroke="#2dcfae" strokeWidth="1.5" />
          <polygon points="14,6 10,17 14,14 18,17" fill="#2dcfae" />
        </svg>
        <div>
          <div className="text-[15px] font-bold tracking-wide text-white">NEXTRO 디지털 트윈</div>
          <div className="text-[11px] text-[#8b949e]">석탄 미분기 (Bowl Mill) 모니터링 시스템</div>
        </div>
      </div>

      <div className="flex items-center gap-5">
        <div className="flex overflow-hidden rounded border border-[#30363d] text-[11px]">
          <button
            type="button"
            onClick={() => setDataMode('live')}
            className={`px-2.5 py-1 font-medium ${
              dataMode === 'live' ? 'bg-[#2dcfae] text-[#0d1117]' : 'text-[#8b949e]'
            }`}
          >
            LIVE
          </button>
          <button
            type="button"
            onClick={() => setDataMode('scenario')}
            className={`px-2.5 py-1 font-medium ${
              dataMode === 'scenario' ? 'bg-[#f0b429] text-[#0d1117]' : 'text-[#8b949e]'
            }`}
          >
            SCENARIO
          </button>
        </div>

        <div className="text-xs text-[#8b949e]">한국중부발전 보령 3호기 · Unit {MACHINE_ID.split('-').pop()}</div>

        <div className="flex items-center gap-1.5 text-xs text-[#2dcfae]">
          <div className="h-[7px] w-[7px] animate-pulse rounded-full bg-[#2dcfae]" />
          실시간 모니터링 중
        </div>

        <div className="font-mono text-xs tabular-nums text-[#8b949e]">{clock}</div>
      </div>
    </header>
  );
}
