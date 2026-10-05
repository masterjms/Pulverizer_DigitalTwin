import { fetchStatus } from '../../api/client';
import { usePolling } from '../../hooks/usePolling';
import { MetricCard } from './MetricCard';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

// 랜딩 화면 - 미분기 전체 운전변수 중심 레이아웃.
// 입력데이터셋 설명_미분기_20260604.xlsx 의 14개 태그를 그대로 반영한다.
// 트렌드 차트는 이 패널(우측, 폭 380px)이 아니라 화면 하단 전체 너비 스트립
// (TrendStrip, App.tsx에서 렌더링)에 그린다 - 여기 넣기엔 그래프가 너무 좁아진다.
export function OverviewPanel() {
  const { data: status } = usePolling(() => fetchStatus(MACHINE_ID), 1000);

  return (
    <div className="flex h-full flex-col gap-3.5 overflow-y-auto p-3.5">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-sm font-bold text-white">
          <div className="h-[18px] w-1 rounded-sm bg-[#2dcfae]" />
          전체 운전 변수 — {MACHINE_ID}
        </h2>
        <span
          className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
            status?.alarmLevel === 'ALARM'
              ? 'bg-[#ef4444]/18 text-[#ef4444]'
              : status?.alarmLevel === 'WARNING'
                ? 'bg-[#f0b429]/18 text-[#f0b429]'
                : 'bg-[#2dcfae]/18 text-[#2dcfae]'
          }`}
        >
          {status?.alarmLevel ?? '...'}
        </span>
      </div>

      <div className="grid grid-cols-2 gap-1.5">
        <MetricCard label="발전 출력" value={status?.mw ?? '-'} unit="MW" />
        <MetricCard label="급탄량" value={status?.feedRateTph ?? '-'} unit="T/H" />
        <MetricCard label="미분기 모터전류" value={status?.motorCurrent ?? '-'} unit="A" />
        <MetricCard label="입구온도" value={status?.inletTempC ?? '-'} unit="℃" />
        <MetricCard label="출구온도" value={status?.outletTempC ?? '-'} unit="℃" />
        <MetricCard label="Bowl dP" value={status?.bowlDpMmH2o ?? '-'} unit="mmH2O" />
        <MetricCard label="분류기 Speed Demand" value={status?.classifierSpeedDemandPct ?? '-'} unit="%" />
        <MetricCard label="분류기 실속도" value={status?.classifierVaneSpeedActualRpm ?? '-'} unit="RPM" />
        <MetricCard label="분류기 Speed Bias" value={status?.classifierSpeedBiasPct ?? '-'} unit="%" />
        <MetricCard
          label="Journal Pressure 설정"
          value={status?.journalPressureSetpointPsi ?? '-'}
          unit="psi"
        />
        <MetricCard
          label="Journal Pressure 현재"
          value={status?.journalPressureActualPsi ?? '-'}
          unit="psi"
        />
        <MetricCard
          label="Pyrite Hopper 밸브"
          value={status?.pyriteValveOpen ? '열림' : '닫힘'}
          tone={status?.pyriteValveOpen ? 'warning' : 'normal'}
        />
        <MetricCard label="Pyrite Hopper 온도" value={status?.pyriteHopperTempC ?? '-'} unit="℃" />
        <MetricCard
          label="누적 운전시간"
          value={status?.cumulativeOperatingHours?.toLocaleString() ?? '-'}
          unit="h"
        />
      </div>

      <p className="mt-auto text-[11px] text-[#484f58]">
        롤타이어를 클릭하면 해당 타이어의 마모예측/RUL 화면으로 전환됩니다.
      </p>
    </div>
  );
}
