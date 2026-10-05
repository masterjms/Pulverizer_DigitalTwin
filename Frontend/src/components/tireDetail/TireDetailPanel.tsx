import { fetchWear } from '../../api/client';
import { fetchStatus } from '../../api/client';
import { usePolling } from '../../hooks/usePolling';
import { useTwinStore } from '../../store/twinStore';
import type { ContactWidthScenario } from '../../types/api';
import { damageTone, toneColor } from '../../theme';
import { HeatmapBarRow } from './HeatmapBarRow';
import { MiniStatusGraph } from './MiniStatusGraph';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

const SCENARIO_LABELS: Record<ContactWidthScenario, string> = {
  band: '① 밴드 집중 (12.7mm)',
  full_width: '② 전폭 분산 (483mm)',
  partial_sweep_30pct: '③ 부분 스윕 30%',
};

// ovzexx/CoalPulverizer3D 레퍼런스의 우측 패널을 그대로 이식 - 단, 실제 롤타이어는
// 3개 중 1개만 측정/교체 판단하므로 b-1/b-2/b-3 개별 카드 대신 대표 1개로 단순화했다.
export function TireDetailPanel() {
  const scenario = useTwinStore((state) => state.scenario);
  const setScenario = useTwinStore((state) => state.setScenario);
  const clearSelection = useTwinStore((state) => state.clearSelection);

  const { data: wearResponse } = usePolling(
    () => fetchWear(MACHINE_ID, { contactWidthScenario: scenario.contactWidthScenario }),
    2000,
    [scenario.contactWidthScenario],
  );
  const { data: status } = usePolling(() => fetchStatus(MACHINE_ID), 1000);

  const tire = wearResponse?.tire;

  if (!tire) {
    return <p className="p-4 text-xs text-[#8b949e]">마모 데이터를 불러오는 중...</p>;
  }

  const tone = damageTone(tire.damagePercent);
  const isUrgent = tire.damagePercent >= 70 || (tire.remainingUsefulLifeHours ?? Infinity) < 1000;
  const toneCss = toneColor[tone];
  const tempDrop = status ? status.inletTempC - status.outletTempC : null;

  return (
    <div className="h-full overflow-y-auto p-3.5">
      {/* 최우선 경고: 점검 필요 + 교체 예측 */}
      <div
        className="mb-2.5 rounded-lg border p-2.5"
        style={{
          borderColor: `${toneCss}59`,
          background: `${toneCss}1A`,
        }}
      >
        <div className="mb-1.5 flex items-center gap-1.5 text-xs font-bold" style={{ color: toneCss }}>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke={toneCss} strokeWidth="2.5">
            <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
          </svg>
          <span>롤타이어 — {isUrgent ? '즉시 점검 필요' : tone === 'good' ? '정상 범위' : '주의 관찰 권장'}</span>
        </div>
        <div className="text-[11px] leading-relaxed text-[#c9d1d9]">
          손상도 <strong style={{ color: toneCss }}>{tire.damagePercent.toFixed(0)}%</strong>
          {' · '}
          잔존수명 <strong style={{ color: toneCss }}>{tire.remainingUsefulLifeHours?.toLocaleString() ?? '-'}시간</strong>
        </div>
      </div>

      <div className="mb-3 flex items-center justify-between">
        <div className="flex items-center gap-2 text-sm font-bold text-white">
          <div className="h-[18px] w-1 rounded-sm bg-[#2dcfae]" />
          롤 타이어 — 상태·마모
        </div>
        <button type="button" onClick={clearSelection} className="text-base text-[#484f58] hover:text-[#ef4444]">
          ✕
        </button>
      </div>

      {/* 마모/손상 */}
      <div className="mb-3.5 mt-3.5 text-[10px] uppercase tracking-wide text-[#8b949e]">마모 / 손상</div>
      <div className="grid grid-cols-2 gap-1.5">
        <MetricCard label="예측 마모(평균)" value={tire.wearMm.toFixed(1)} unit={`mm / 한계 ${tire.wearLimitMm}mm`} color={toneCss} />
        <MetricCard label="손상도" value={tire.damagePercent.toFixed(1)} unit="%" color={toneCss} />
        <MetricCard label="마모율" value={tire.wearRateMmPerHour.toFixed(5)} unit="mm/h" color="#e6edf3" />
        <MetricCard label="심각도 배수" value={tire.severityMultiplier.toFixed(2)} unit="× (기준 하중 대비)" color={toneCss} />
      </div>

      {/* RUL */}
      <div className="mb-1.5 mt-3.5 text-[10px] uppercase tracking-wide text-[#8b949e]">잔여수명(RUL) · 교체 예측</div>
      <div className="rounded-lg border border-[#2a4a6a] bg-[#1a2535] px-2.5 py-2">
        <div className="mb-1 text-[10px] font-semibold text-[#4a8aaa]">롤타이어 (예측 잔존수명)</div>
        <div className="text-xl font-extrabold leading-none text-[#2dcfae]">
          {tire.remainingUsefulLifeHours?.toLocaleString() ?? '-'}
        </div>
        <div className="mt-0.5 text-[9px] text-[#6a8aaa]">시간 남음 (현재 마모율 기준 선형 추정)</div>
      </div>

      {/* 마모 히트맵 */}
      <div className="mb-1.5 mt-3.5 text-[10px] uppercase tracking-wide text-[#8b949e]">마모 히트맵 — Zone 1~10</div>
      <HeatmapBarRow
        zoneWearProfileMm={tire.zoneWearProfileMm}
        wearLimitMm={tire.wearLimitMm}
        avgPercent={tire.damagePercent}
      />

      {/* 실시간 미니 그래프 */}
      <MiniStatusGraph />

      {/* 운전 변수 */}
      <div className="mb-1.5 mt-3.5 text-[10px] uppercase tracking-wide text-[#8b949e]">운전 변수</div>
      <div className="grid grid-cols-2 gap-1.5">
        <MetricCard label="부하" value={status?.mw.toFixed(0) ?? '-'} unit="MW" color="#2dcfae" />
        <MetricCard label="온도강하" value={tempDrop?.toFixed(1) ?? '-'} unit="℃ (입구-출구)" color="#e6edf3" />
        <MetricCard label="급탄율" value={status?.feedRateTph.toFixed(1) ?? '-'} unit="t/h" color="#e6edf3" />
        <MetricCard label="저널 압력" value={status?.journalPressureActualPsi.toFixed(0) ?? '-'} unit="psi" color="#e6edf3" />
        <MetricCard label="보울 차압" value={status?.bowlDpMmH2o.toFixed(0) ?? '-'} unit="mmH₂O" color="#e6edf3" />
        <MetricCard label="분급기 속도" value={status?.classifierVaneSpeedActualRpm.toFixed(1) ?? '-'} unit="rpm" color="#e6edf3" />
      </div>

      {/* SCENARIO: 접촉폭 시나리오 */}
      <div className="mb-1.5 mt-3.5 text-[10px] uppercase tracking-wide text-[#8b949e]">접촉폭(w) 시나리오 — SCENARIO 모드</div>
      <div className="flex flex-col gap-1.5">
        {(Object.keys(SCENARIO_LABELS) as ContactWidthScenario[]).map((key) => (
          <button
            key={key}
            type="button"
            onClick={() => setScenario({ contactWidthScenario: key })}
            className={`rounded border px-2 py-1.5 text-left text-xs transition-colors ${
              scenario.contactWidthScenario === key
                ? 'border-[#2dcfae] bg-[#2dcfae]/10 text-[#2dcfae]'
                : 'border-[#30363d] text-[#8b949e] hover:bg-[#21262d]'
            }`}
          >
            {SCENARIO_LABELS[key]}
          </button>
        ))}
      </div>

      <div className="mb-2 mt-2.5 rounded-lg border border-[#f0b429]/25 bg-[#f0b429]/[0.08] p-2.5 text-[10px] leading-relaxed text-[#8b949e]">
        <div className="mb-1 font-semibold text-[#f0b429]">하중(F) 추정 방식: {tire.loadEstimationMethod}</div>
        <ul className="list-inside list-disc space-y-0.5">
          {tire.notes.map((note) => (
            <li key={note}>{note}</li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function MetricCard({ label, value, unit, color }: { label: string; value: string; unit: string; color: string }) {
  return (
    <div className="rounded-md bg-[#21262d] px-2.5 py-1.5">
      <div className="mb-0.5 text-[9px] text-[#8b949e]">{label}</div>
      <div className="text-base font-extrabold leading-tight" style={{ color }}>
        {value}
      </div>
      <div className="text-[9px] text-[#8b949e]">{unit}</div>
    </div>
  );
}
