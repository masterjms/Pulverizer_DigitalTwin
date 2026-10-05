import { useEffect, useRef } from 'react';
import { fetchStatus } from '../../api/client';
import { usePolling } from '../../hooks/usePolling';
import { wearColor } from '../../theme';

const MACHINE_ID = import.meta.env.VITE_MACHINE_ID ?? 'PV-001';

interface EngineeringDrawingTabProps {
  zoneWearProfileMm: number[];
  wearLimitMm: number;
}

// ovzexx/CoalPulverizer3D 레퍼런스의 "엔지니어링 도면" 탭 SVG를 그대로 이식.
// 좌: 보울 허브 원뿔 단면(내측 컨텍스트), 중앙: 롤 타이어 단면(Zone 1~10),
// 우: 압력 하우징 벽체(외측 컨텍스트) + 클리어런스 표시. 타이어 회전(sweep 밴드)은
// 실제 Bowl RPM에 연동해 애니메이션한다.
const ZX = [185, 223, 261, 299, 337, 375, 413, 451, 489, 527, 565];
const TY = ZX.map((_, i) => 68 - 3 * i);
const BY = 157;
const TIRE_TOP = 38;
const TIRE_BOT = 157;
const TIRE_H = TIRE_BOT - TIRE_TOP;
const RECT_H = TIRE_H * 3;
const TRAVEL = RECT_H + TIRE_H;

export function EngineeringDrawingTab({ zoneWearProfileMm, wearLimitMm }: EngineeringDrawingTabProps) {
  const { data: status } = usePolling(() => fetchStatus(MACHINE_ID), 2000);
  const sweepRef = useRef<SVGRectElement>(null);
  const phaseRef = useRef(0);
  const lastRef = useRef<number | null>(null);
  const rpmRef = useRef(41.6);

  useEffect(() => {
    if (status) rpmRef.current = status.bowlRpm;
  }, [status]);

  useEffect(() => {
    let raf = 0;
    const tick = (ts: number) => {
      if (lastRef.current === null) lastRef.current = ts;
      const dt = (ts - lastRef.current) / 1000;
      lastRef.current = ts;
      const period = 60 / (Math.max(rpmRef.current, 5) * 0.8);
      phaseRef.current = (phaseRef.current + dt / period) % 1;
      if (sweepRef.current) {
        const y = TIRE_TOP - RECT_H + phaseRef.current * TRAVEL;
        sweepRef.current.setAttribute('y', y.toFixed(1));
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, []);

  const zoneCount = zoneWearProfileMm.length || 10;

  return (
    <div className="flex h-full gap-3.5 p-3">
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="mb-1.5 text-[11px] text-[#8b949e]">
          롤 타이어 단면도 (Roll Tire Cross-Section) — Zone 1: 내측(좁은 끝) → Zone {zoneCount}: 외측(넓은 끝)
        </div>
        <div className="flex flex-1 items-center justify-center overflow-hidden rounded-lg border border-[#21262d] bg-[#08111a] p-2.5">
          <svg viewBox="0 0 700 215" className="h-full max-h-[215px] w-full">
            <defs>
              <pattern id="egrid" width="20" height="20" patternUnits="userSpaceOnUse">
                <path d="M20 0L0 0 0 20" fill="none" stroke="#0d1f2f" strokeWidth="0.5" />
              </pattern>
              <pattern id="hatch" x="0" y="0" width="7" height="7" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
                <line x1="0" y1="0" x2="0" y2="7" stroke="#2a4830" strokeWidth="1" />
              </pattern>
              <pattern id="ctx-hatch" x="0" y="0" width="7" height="7" patternUnits="userSpaceOnUse" patternTransform="rotate(-45)">
                <line x1="0" y1="0" x2="0" y2="7" stroke="#253040" strokeWidth="1" />
              </pattern>
              <clipPath id="tire-clip">
                <polygon points="185,68 565,38 565,157 185,157" />
              </clipPath>
              <linearGradient id="sweep-grad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#fff" stopOpacity="0" />
                <stop offset="38%" stopColor="#fff" stopOpacity="0" />
                <stop offset="48%" stopColor="#fff" stopOpacity="0.30" />
                <stop offset="52%" stopColor="#fff" stopOpacity="0.30" />
                <stop offset="62%" stopColor="#fff" stopOpacity="0" />
                <stop offset="100%" stopColor="#fff" stopOpacity="0" />
              </linearGradient>
              <marker id="arr" markerWidth="5" markerHeight="5" refX="5" refY="2.5" orient="auto">
                <polygon points="0,0 5,2.5 0,5" fill="#484f58" />
              </marker>
              <marker id="arr2" markerWidth="5" markerHeight="5" refX="0" refY="2.5" orient="auto">
                <polygon points="5,0 0,2.5 5,5" fill="#484f58" />
              </marker>
            </defs>

            <rect width="760" height="215" fill="url(#egrid)" />

            {/* 좌: 보울 허브 원뿔 단면 (내측 컨텍스트) */}
            <line x1="8" y1="10" x2="8" y2="175" stroke="#3a5a7a" strokeWidth="1" strokeDasharray="6,4" />
            <polygon points="8,25 183,25 8,157" fill="url(#ctx-hatch)" stroke="#6a8aaa" strokeWidth="1.5" />
            <text x="72" y="185" fill="#6a8aaa" fontSize="8" textAnchor="middle">Bowl Hub (내측)</text>

            {/* 롤 타이어 본체 */}
            <polygon points="185,68 565,38 565,157 185,157" fill="url(#hatch)" />
            <line x1="185" y1="68" x2="565" y2="38" stroke="#c9d1d9" strokeWidth="2.2" />
            <line x1="185" y1="157" x2="565" y2="157" stroke="#e8a020" strokeWidth="2.8" />
            <line x1="185" y1="68" x2="185" y2="157" stroke="#c9d1d9" strokeWidth="2" />
            <line x1="565" y1="38" x2="565" y2="157" stroke="#c9d1d9" strokeWidth="2" />

            {/* Zone 마모 오버레이 */}
            <g opacity={0.82}>
              {Array.from({ length: zoneCount }, (_, i) => {
                const wearMm = zoneWearProfileMm[i] ?? 0;
                const percent = wearLimitMm > 0 ? (wearMm / wearLimitMm) * 100 : 0;
                const cx = (ZX[i] + ZX[i + 1]) / 2;
                const topAvg = (TY[i] + TY[i + 1]) / 2;
                const cy = (topAvg + BY) / 2 + 4;
                const label =
                  percent >= 80
                    ? { fill: '#ffef00', stroke: '#7a0000', sw: 1.0, sz: 15 }
                    : percent >= 60
                      ? { fill: '#ffffff', stroke: '#4a1500', sw: 0.7, sz: 14 }
                      : percent >= 30
                        ? { fill: '#ffffff', stroke: '#2a1800', sw: 0.4, sz: 13 }
                        : { fill: '#ffffff', stroke: '#0b4033', sw: 0.3, sz: 12 };
                return (
                  <g key={i}>
                    <polygon
                      points={`${ZX[i]},${TY[i]} ${ZX[i + 1]},${TY[i + 1]} ${ZX[i + 1]},${BY} ${ZX[i]},${BY}`}
                      fill={wearColor(percent)}
                    />
                    <text
                      x={cx}
                      y={cy}
                      textAnchor="middle"
                      fontSize={label.sz}
                      fontWeight={800}
                      fontFamily="monospace"
                      fill={label.fill}
                      stroke={label.stroke}
                      strokeWidth={label.sw}
                      paintOrder="stroke"
                    >
                      {wearMm.toFixed(1)}
                    </text>
                  </g>
                );
              })}
            </g>

            {/* 회전 sweep 오버레이 */}
            <rect
              ref={sweepRef}
              x={185}
              width={380}
              y={38}
              height={357}
              fill="url(#sweep-grad)"
              clipPath="url(#tire-clip)"
              pointerEvents="none"
            />

            {/* Zone 구분선 */}
            <g stroke="#2dcfae" strokeWidth={0.8} strokeDasharray="3,4" opacity={0.55}>
              {ZX.slice(1, -1).map((x, i) => (
                <line key={i} x1={x} y1={TY[i + 1]} x2={x} y2={157} />
              ))}
            </g>

            {/* Zone 번호 */}
            <g fill="#2dcfae" fontSize="10" fontFamily="monospace" textAnchor="middle" fontWeight="bold">
              {Array.from({ length: zoneCount }, (_, i) => (
                <text key={i} x={(ZX[i] + ZX[i + 1]) / 2} y={174}>
                  {i + 1}
                </text>
              ))}
            </g>

            {/* 접촉면 레이블 */}
            <line x1="375" y1="159" x2="375" y2="171" stroke="#e8a020" strokeWidth="0.8" />
            <text x="375" y="182" fill="#e8a020" fontSize="8.5" textAnchor="middle">
              접촉면 / Contact Face (분쇄 구역)
            </text>

            {/* 우: 압력 하우징 벽체 (외측 컨텍스트) */}
            <rect x="600" y="22" width="52" height="142" fill="url(#ctx-hatch)" stroke="#6a8aaa" strokeWidth="1.5" />
            <line x1="600" y1="22" x2="600" y2="164" stroke="#8aaabb" strokeWidth="2" />
            <text x="582" y="185" fill="#6a8aaa" fontSize="8" textAnchor="middle">Housing</text>
            <text x="582" y="195" fill="#6a8aaa" fontSize="8" textAnchor="middle">외측 벽체</text>
            <line x1="567" y1="93" x2="598" y2="93" stroke="#4a7a9a" strokeWidth="0.8" markerEnd="url(#arr)" markerStart="url(#arr2)" />
            <text x="582" y="88" fill="#4a7a9a" fontSize="7.5" textAnchor="middle">클리어런스</text>

            {/* 치수선 */}
            <line x1="185" y1="12" x2="565" y2="12" stroke="#30363d" strokeWidth="0.8" markerEnd="url(#arr)" markerStart="url(#arr2)" />
            <text x="375" y="10" fill="#484f58" fontSize="8" textAnchor="middle">롤 타이어 전체 폭 (10 구간)</text>
            <line x1="185" y1="200" x2="565" y2="200" stroke="#30363d" strokeWidth="1" markerEnd="url(#arr)" markerStart="url(#arr2)" />
            <text x="375" y="212" fill="#484f58" fontSize="8.5" textAnchor="middle">
              ← Zone 1 (내측 · 좁은 끝)　　Zone {zoneCount} (외측 · 넓은 끝) →
            </text>
          </svg>
        </div>
        <p className="mt-1 text-[10px] text-[#484f58]">
          Zone별 수치는 실측이 아니라, 전체 평균 마모량과 접촉폭(w) 시나리오로 만든 예시 분포입니다.
        </p>
      </div>
    </div>
  );
}
