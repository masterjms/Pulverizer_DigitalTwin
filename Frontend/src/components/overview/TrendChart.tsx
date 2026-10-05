import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { HistoryPoint } from '../../types/api';

interface TrendChartProps {
  title: string;
  data: HistoryPoint[];
  dataKey: string;
  color: string;
  unit?: string;
  height?: number;
}

export function TrendChart({ title, data, dataKey, color, unit, height = 140 }: TrendChartProps) {
  return (
    <div className="flex h-full flex-col rounded-lg border border-[#21262d] bg-[#08111a] p-3">
      <div className="mb-2 text-[11px] text-[#8b949e]">
        {title} {unit && <span className="text-[#484f58]">({unit})</span>}
      </div>
      <ResponsiveContainer width="100%" height={height}>
        <LineChart data={data}>
          <XAxis dataKey="time" hide />
          <YAxis width={36} tick={{ fontSize: 10, fill: '#8b949e' }} />
          <Tooltip
            contentStyle={{ background: '#21262d', border: '1px solid #30363d', fontSize: 12, color: '#e6edf3' }}
            labelFormatter={(label) => new Date(label).toLocaleTimeString()}
          />
          <Line type="monotone" dataKey={dataKey} stroke={color} dot={false} strokeWidth={2} isAnimationActive={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
