import { wearColor } from '../../theme';

interface HeatmapBarRowProps {
  zoneWearProfileMm: number[];
  wearLimitMm: number;
  avgPercent: number;
}

// ovzexx 레퍼런스의 우측 패널 "마모 히트맵 — Zone 1~10" 미니 바.
export function HeatmapBarRow({ zoneWearProfileMm, wearLimitMm, avgPercent }: HeatmapBarRowProps) {
  return (
    <div className="mb-2.5">
      <div className="mb-1 flex justify-between text-[10px] text-[#8b949e]">
        <span className="font-bold text-[#e6edf3]">롤타이어</span>
        <span>예측평균 {avgPercent.toFixed(0)}%</span>
      </div>
      <div className="flex h-3.5 gap-0.5">
        {zoneWearProfileMm.map((wearMm, i) => {
          const percent = wearLimitMm > 0 ? (wearMm / wearLimitMm) * 100 : 0;
          return (
            <div
              key={i}
              title={`Z${i + 1}: ${percent.toFixed(0)}%`}
              className="flex-1 cursor-pointer rounded-sm transition-opacity hover:opacity-75"
              style={{ backgroundColor: wearColor(percent) }}
            />
          );
        })}
      </div>
    </div>
  );
}
