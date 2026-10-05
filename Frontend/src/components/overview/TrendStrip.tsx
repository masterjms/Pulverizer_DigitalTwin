import { fetchHistory } from '../../api/client';
import { usePolling } from '../../hooks/usePolling';
import { TrendChart } from './TrendChart';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

// 화면 하단 전체 너비를 쓰는 트렌드 차트 스트립. 우측 사이드패널(380px) 안에 넣으면
// 그래프 폭이 너무 좁아서 추세가 안 보이므로, 뷰어+사이드패널 아래에 별도 행으로 둔다.
export function TrendStrip() {
  const { data: history } = usePolling(() => fetchHistory(MACHINE_ID, 15), 5000);

  return (
    <div className="grid h-56 shrink-0 grid-cols-1 gap-3 border-t border-[#30363d] bg-[#161b22] p-3 sm:grid-cols-3">
      <TrendChart title="모터전류" data={history?.data ?? []} dataKey="motor_current" color="#2dcfae" unit="A" height={160} />
      <TrendChart title="베어링 온도" data={history?.data ?? []} dataKey="bearing_temp" color="#f0b429" unit="℃" height={160} />
      <TrendChart title="진동" data={history?.data ?? []} dataKey="vibration" color="#ef4444" unit="mm/s" height={160} />
    </div>
  );
}
