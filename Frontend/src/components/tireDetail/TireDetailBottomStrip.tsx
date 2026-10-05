import { fetchWear } from '../../api/client';
import { usePolling } from '../../hooks/usePolling';
import { useTwinStore } from '../../store/twinStore';
import type { BottomTab } from '../../store/twinStore';
import { EngineeringDrawingTab } from './EngineeringDrawingTab';
import { WearHeatmapTab } from './WearHeatmapTab';
import { StatusGraphTab } from './StatusGraphTab';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

const TABS: { key: BottomTab; label: string }[] = [
  { key: 'drawing', label: '엔지니어링 도면' },
  { key: 'heatmap', label: '마모도' },
  { key: 'status', label: '상태 그래프' },
];

// Tire Detail 모드 전용 하단 전체 너비 스트립. ovzexx.github.io/CoalPulverizer3D
// 레퍼런스의 하단 3탭(엔지니어링 도면/마모도/상태 그래프)을 그대로 따른다.
export function TireDetailBottomStrip() {
  const activeTab = useTwinStore((state) => state.bottomTab);
  const setBottomTab = useTwinStore((state) => state.setBottomTab);
  const scenario = useTwinStore((state) => state.scenario);

  const { data: wearResponse } = usePolling(
    () => fetchWear(MACHINE_ID, { contactWidthScenario: scenario.contactWidthScenario }),
    2000,
    [scenario.contactWidthScenario],
  );

  const tire = wearResponse?.tire;

  return (
    <div className="flex h-[290px] shrink-0 flex-col border-t border-[#30363d] bg-[#161b22]">
      <div className="flex gap-1 border-b border-[#21262d] px-3.5 pt-1">
        {TABS.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setBottomTab(tab.key)}
            className={`border-b-2 px-4 py-2 text-xs transition-colors ${
              activeTab === tab.key
                ? 'border-[#2dcfae] text-[#2dcfae]'
                : 'border-transparent text-[#8b949e] hover:text-[#e6edf3]'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="min-h-0 flex-1">
        {!tire && <p className="p-3 text-xs text-[#8b949e]">마모 데이터를 불러오는 중...</p>}
        {tire && activeTab === 'drawing' && (
          <EngineeringDrawingTab zoneWearProfileMm={tire.zoneWearProfileMm} wearLimitMm={tire.wearLimitMm} />
        )}
        {tire && activeTab === 'heatmap' && (
          <WearHeatmapTab zoneWearProfileMm={tire.zoneWearProfileMm} wearLimitMm={tire.wearLimitMm} />
        )}
        {activeTab === 'status' && <StatusGraphTab />}
      </div>
    </div>
  );
}
