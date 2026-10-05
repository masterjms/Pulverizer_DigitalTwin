import { wearColor } from '../../theme';

interface WearHeatmapTabProps {
  zoneWearProfileMm: number[];
  wearLimitMm: number;
}

// ovzexx 레퍼런스의 "마모도" 탭 - zone별 마모율 히트맵.
// 실제 현장은 롤타이어 3개(b-1/b-2/b-3) 행이 있지만, 우리는 1개만 측정하므로 한 행만 그린다.
export function WearHeatmapTab({ zoneWearProfileMm, wearLimitMm }: WearHeatmapTabProps) {
  const zoneCount = zoneWearProfileMm.length || 10;

  return (
    <div className="flex h-full flex-col gap-2 p-3">
      <div className="text-[11px] text-[#8b949e]">
        롤타이어 마모도 히트맵 — Zone 1(내측) → Zone {zoneCount}(외측)
      </div>

      <div className="flex flex-1 items-center gap-1.5">
        <div className="w-7 shrink-0 text-right text-[11px] text-[#8b949e]">롤</div>
        <div className="flex flex-1 gap-0.5">
          {zoneWearProfileMm.map((wearMm, i) => {
            const percent = wearLimitMm > 0 ? (wearMm / wearLimitMm) * 100 : 0;
            return (
              <div
                key={i}
                title={`Zone ${i + 1}: ${percent.toFixed(0)}%`}
                className="h-10 flex-1 cursor-pointer rounded-[3px] transition-transform hover:scale-y-110"
                style={{ backgroundColor: wearColor(percent) }}
              />
            );
          })}
        </div>
      </div>

      <div className="flex gap-0.5 pl-[34px]">
        {Array.from({ length: zoneCount }, (_, i) => (
          <div key={i} className="flex-1 text-center text-[9px] text-[#484f58]">
            {i + 1}
          </div>
        ))}
      </div>

      <div className="mt-1 flex items-center gap-2 text-[10px] text-[#8b949e]">
        <span>정상 (0%)</span>
        <div className="h-[7px] flex-1 rounded-sm" style={{ background: 'linear-gradient(to right,#2dcfae,#f0b429,#ef4444)' }} />
        <span>교체 (100%)</span>
      </div>
    </div>
  );
}
