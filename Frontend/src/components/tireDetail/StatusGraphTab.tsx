import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { fetchStatus } from '../../api/client';
import { useRollingBuffer } from '../../hooks/useRollingBuffer';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';
const WINDOW_SIZE = 60;

interface StatusSample {
  time: number;
  journalPressure: number;
  vibration: number;
}

// ovzexx 레퍼런스의 "상태 그래프" 탭 - 하중(Journal Pressure로 근사)과 진동을
// 최근 60초 실시간으로 보여준다. 아직 이 필드들은 DB에 저장되지 않으므로
// (Phase 1 범위 밖) 클라이언트에서 폴링하며 직접 60개짜리 롤링 버퍼를 만든다.
export function StatusGraphTab() {
  const samples = useRollingBuffer<Awaited<ReturnType<typeof fetchStatus>>, StatusSample>(
    () => fetchStatus(MACHINE_ID),
    (data) => ({
      time: Date.parse(data.timestamp),
      journalPressure: data.journalPressureActualPsi,
      vibration: data.vibration,
    }),
    1000,
    WINDOW_SIZE,
  );

  const latest = samples[samples.length - 1];

  return (
    <div className="flex h-full gap-3.5 p-3">
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="mb-1.5 text-[11px] text-[#8b949e]">
          Journal Pressure(하중 프록시) &amp; 진동 — 실시간 ({WINDOW_SIZE}초)
        </div>
        <div className="flex-1 rounded-lg border border-[#21262d] bg-[#08111a]">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={samples} margin={{ top: 12, right: 12, bottom: 4, left: 4 }}>
              <XAxis dataKey="time" hide />
              <YAxis yAxisId="left" width={40} tick={{ fontSize: 10, fill: '#8b949e' }} />
              <YAxis yAxisId="right" orientation="right" width={32} tick={{ fontSize: 10, fill: '#8b949e' }} />
              <Tooltip
                contentStyle={{ background: '#21262d', border: '1px solid #30363d', fontSize: 12, color: '#e6edf3' }}
                labelFormatter={(label) => new Date(label).toLocaleTimeString()}
              />
              <Line
                yAxisId="left"
                type="monotone"
                dataKey="journalPressure"
                name="Journal Pr (psi)"
                stroke="#2dcfae"
                dot={false}
                strokeWidth={1.8}
                isAnimationActive={false}
              />
              <Line
                yAxisId="right"
                type="monotone"
                dataKey="vibration"
                name="진동 (mm/s)"
                stroke="#f0b429"
                dot={false}
                strokeWidth={1.8}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="flex w-[150px] shrink-0 flex-col gap-2">
        <StatCard label="Journal Pressure" value={latest?.journalPressure} unit="psi" color="#2dcfae" />
        <StatCard label="진동" value={latest?.vibration} unit="mm/s" color="#f0b429" />
      </div>
    </div>
  );
}

function StatCard({ label, value, unit, color }: { label: string; value?: number; unit: string; color: string }) {
  return (
    <div className="rounded-lg bg-[#21262d] px-3 py-2.5">
      <div className="text-[10px] text-[#8b949e]">{label}</div>
      <div className="mt-0.5 text-[22px] font-extrabold leading-none" style={{ color }}>
        {value?.toFixed(1) ?? '-'}
      </div>
      <div className="mt-0.5 text-[10px] text-[#8b949e]">{unit}</div>
    </div>
  );
}
