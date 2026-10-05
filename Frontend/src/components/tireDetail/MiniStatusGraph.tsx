import { Line, LineChart, ResponsiveContainer } from 'recharts';
import { fetchStatus } from '../../api/client';
import { useRollingBuffer } from '../../hooks/useRollingBuffer';
import { useTwinStore } from '../../store/twinStore';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

interface Sample {
  journalPressure: number;
  vibration: number;
}

// 우측 패널 하단에 상시 표시되는 작은 실시간 그래프. 클릭하면 하단 스트립의
// "상태 그래프" 탭이 열린다 (ovzexx 레퍼런스의 rp-graph-canvas onclick 패턴).
export function MiniStatusGraph() {
  const setBottomTab = useTwinStore((state) => state.setBottomTab);

  const samples = useRollingBuffer<Awaited<ReturnType<typeof fetchStatus>>, Sample>(
    () => fetchStatus(MACHINE_ID),
    (data) => ({ journalPressure: data.journalPressureActualPsi, vibration: data.vibration }),
    1000,
    60,
  );

  const latest = samples[samples.length - 1];

  return (
    <div className="mt-3.5 border-t border-[#21262d] pt-3">
      <div className="mb-1.5 text-[10px] uppercase tracking-wide text-[#8b949e]">실시간 운전 상태</div>
      <button
        type="button"
        onClick={() => setBottomTab('status')}
        title="클릭하면 상세 그래프 열림"
        className="block h-[90px] w-full rounded-lg border border-[#21262d] bg-[#08111a] hover:border-[#30363d]"
      >
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={samples} margin={{ top: 6, right: 4, bottom: 6, left: 4 }}>
            <Line type="monotone" dataKey="journalPressure" stroke="#2dcfae" dot={false} strokeWidth={1.6} isAnimationActive={false} />
            <Line type="monotone" dataKey="vibration" stroke="#f0b429" dot={false} strokeWidth={1.6} isAnimationActive={false} />
          </LineChart>
        </ResponsiveContainer>
      </button>
      <div className="mt-1 flex justify-between text-[10px]">
        <span className="text-[#2dcfae]">
          압력: <strong>{latest?.journalPressure.toFixed(1) ?? '—'}</strong> psi
        </span>
        <span className="text-[#f0b429]">
          진동: <strong>{latest?.vibration.toFixed(1) ?? '—'}</strong> mm/s
        </span>
      </div>
    </div>
  );
}
