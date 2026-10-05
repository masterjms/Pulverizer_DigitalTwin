// ovzexx/CoalPulverizer3D (NEXTRO 디지털 트윈) 레퍼런스의 색상 그대로.
// className에는 Tailwind arbitrary value(bg-[#0d1117])로 쓰고, SVG/Canvas처럼
// 문자열 색상이 직접 필요한 곳에서는 이 상수를 쓴다.
export const COLORS = {
  bgBase: '#0d1117',
  bgPanel: '#161b22',
  bgCard: '#21262d',
  bgCanvas: '#08111a',
  border: '#30363d',
  borderFaint: '#21262d',
  textPrimary: '#e6edf3',
  textSecondary: '#8b949e',
  textFaint: '#484f58',
  teal: '#2dcfae', // 정상/LIVE
  amber: '#f0b429', // 주의
  red: '#ef4444', // 위험
} as const;

// 마모율(%) -> 색상. wearColor(0~100).
export function wearColor(percent: number): string {
  const p = Math.max(0, Math.min(100, percent));
  if (p < 40) return `hsl(${162 - p * 0.5}deg,58%,${38 + p * 0.2}%)`;
  if (p < 70) return `hsl(${42 - (p - 40) * 0.8}deg,78%,48%)`;
  return `hsl(8deg,80%,${46 - (p - 70) * 0.3}%)`;
}

export function damageTone(percent: number): 'good' | 'warn' | 'danger' {
  if (percent >= 70) return 'danger';
  if (percent >= 40) return 'warn';
  return 'good';
}

export const toneColor: Record<'good' | 'warn' | 'danger', string> = {
  good: COLORS.teal,
  warn: COLORS.amber,
  danger: COLORS.red,
};
