interface MetricCardProps {
  label: string;
  value: string | number;
  unit?: string;
  tone?: 'normal' | 'warning' | 'alarm';
}

const toneClasses: Record<NonNullable<MetricCardProps['tone']>, string> = {
  normal: 'text-[#2dcfae]',
  warning: 'text-[#f0b429]',
  alarm: 'text-[#ef4444]',
};

export function MetricCard({ label, value, unit, tone = 'normal' }: MetricCardProps) {
  return (
    <div className="rounded-md bg-[#21262d] px-2.5 py-1.5">
      <div className="mb-0.5 text-[9px] text-[#8b949e]">{label}</div>
      <div className="flex items-baseline gap-1">
        <span className={`text-base font-extrabold leading-tight ${toneClasses[tone]}`}>{value}</span>
        {unit && <span className="text-[9px] text-[#8b949e]">{unit}</span>}
      </div>
    </div>
  );
}
