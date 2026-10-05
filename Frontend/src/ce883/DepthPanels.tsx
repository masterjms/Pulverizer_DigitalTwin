import { sourceLabel,wearColor } from './contract';
import type { ComponentId } from './contract';
import type { DepthDataset,MachineRecord,Snapshot } from './depthContract';
import { productionPercent,costDifference } from './depthContract';
import {fmt,date,timeLabel} from './depthFormat';
export function Empty({children}:{children:React.ReactNode}){return <p className="dw-empty">{children}</p>;}
export function Executive({data,at}:{data:DepthDataset|null;at:string}){
  const production=data?.production&&Date.parse(data.production.asOf)<=Date.parse(at)&&date(data.production.asOf)===date(at)?data.production:null;
  const costs=data?.costs&&Date.parse(data.costs.asOf)<=Date.parse(at)?data.costs:null;
  const percent=productionPercent(production),difference=costDifference(costs);
  return <><section className="dw-section dw-production"><h2>당일 발전 목표 달성</h2><div className="dw-big">{fmt(percent)}<small>%</small></div>
    <div className="dw-progress"><i style={{width:`${Math.min(100,percent??0)}%`}}/></div>
    {production?<><p>{fmt(production.actualMwh)} / {fmt(production.targetMwh)} MWh</p><small>{production.date} · 3호기 전체 · {sourceLabel[production.source]}<br/>{production.basis}</small></>:<Empty>선택 시점의 발전 목표·누적 실적 미연결.<br/>로컬 스키마에는 순간 출력 MW만 확인되었습니다.</Empty>}
  </section><section className="dw-section"><h2>운영 비용 비교</h2><div className="dw-big">{difference==null?'—':fmt(Math.abs(difference)/10000)}<small>만원 {difference!=null&&difference<0?'증가':'절감'}</small></div>
    {costs?<><dl className="dw-def"><dt>기존 교체 방식</dt><dd>{fmt(costs.baselineKrw/10000)} 만원</dd><dt>검토 운영안</dt><dd>{fmt(costs.plannedKrw/10000)} 만원</dd></dl><small>{costs.period} · {sourceLabel[costs.source]}<br/>{costs.basis}</small></>:<Empty>동일 기간의 기준 비용·계획 비용이 필요합니다. 전력 단가, 부품·정비·정지 비용은 로컬 스키마에서 확인되지 않았습니다.</Empty>}
  </section><section className="dw-section"><h2>의사결정 기준</h2><p>설비를 선택해 교체 시기와 상태를 확인하세요.</p><small>발전소 MW는 6대 공통 출력입니다. 미분기별 값을 합산하지 않습니다. 비용 절감과 생산 가능 여부는 검증된 계획 결과를 연결해 판단합니다.</small></section></>;
}
export function Replacement({snapshot,baseline,at}:{snapshot:Snapshot|null;baseline:number;at:string}){
  const r=snapshot?.reliability;const limit=snapshot?.wear?.wearLimitMm;
  const valid=snapshot?.wear?.rollers.flatMap(row=>row.wearMm.filter((_,i)=>row.valid[i]))??[];
  const max=valid.length?Math.max(...valid):null;const ratio=max!=null&&limit?max/limit*100:null;
  const days=r?.replacementAt?Math.ceil((Date.parse(r.replacementAt)-Date.parse(at))/86400000):null;
  return <section className="dw-section dw-replacement"><h2>교체 판단 <span>{snapshot?sourceLabel[snapshot.source]:'미연결'}</span></h2>
    <div className="dw-replacement-main"><div><small>최대 위치 / 마모 한계</small><strong>{fmt(ratio,0)}<em>%</em></strong></div><div><small>교체 권장 시점</small><strong>{days===null?'미산정':days>=0?`D-${days}`:`D+${-days}`}</strong></div></div>
    <p className="dw-replacement-date">{date(r?.replacementAt)} <span>{fmt(r?.remainingHours,0)} 운전시간 남음</span></p>
    <dl className="dw-metric-row"><div><dt>평균 마모</dt><dd>{fmt(r?.wearMeanMm)} <small>mm</small></dd></div><div><dt>마모율</dt><dd>{fmt(r?.wearRateUmPerHour,2)} <small>µm/h</small></dd></div><div><dt>마모 한계</dt><dd>{fmt(limit,0)} <small>mm</small></dd></div></dl>
    <div className="dw-baseline"><p>관행 {fmt(baseline,0)} h 대비 <b>{fmt(r?.cycleOperatingHours,0)} h 운전</b></p><div className="dw-progress"><i style={{width:`${Math.min(100,(r?.cycleOperatingHours??0)/baseline*100)}%`}}/></div></div>
    <small>{r?.basis||'교체 이후 누적 운전시간과 서버의 RUL·교체일 산출값이 필요합니다.'}<br/>운전시간과 달력일은 구분합니다. 20,000 h 기준은 수명 예측값이 아닙니다.</small>
  </section>;
}
export function ZoneMatrix({snapshot,selection,onSelect}:{snapshot:Snapshot|null;selection:{partId:ComponentId;zone:number}|null;onSelect:(partId:ComponentId,zone:number)=>void}){
  const wear=snapshot?.wear;return <section className="dw-section"><h2>롤타이어 위치별 마모 <span>3 × 10</span></h2><div className="dw-zone-header"><span/> {Array.from({length:10},(_,i)=><span key={i}>{i+1}</span>)}</div>
    {(['tire_01','tire_02','tire_03'] as ComponentId[]).map((id,index)=>{const row=wear?.rollers.find(r=>r.componentId===id);return <div className="dw-zone-row" key={id}><button onClick={()=>onSelect(id,0)}>롤 {index+1}</button>{Array.from({length:10},(_,i)=><button key={i} aria-label={`롤 ${index+1} 위치 ${i+1}`} title={row?.valid[i]?`${fmt(row.wearMm[i])} mm`:'데이터 없음'} className={selection?.partId===id&&selection.zone===i+1?'selected':''} style={{background:row&&wear?wearColor(row.wearMm[i],wear.wearLimitMm,row.valid[i]):'#4f5c63'}} onClick={()=>onSelect(id,i+1)}>{row?.valid[i]?fmt(row.wearMm[i],0):'—'}</button>)}</div>;})}
    <small>단위 mm · 회색: 데이터 없음 · 색은 Unity와 동일<br/>내측 좁은 끝 1 → 외측 넓은 끝 10 / 현장 방향 확인 필요</small>
    <div className="dw-legend"><i style={{background:'#33a392'}}/>35% 미만 <i style={{background:'#dbb844'}}/>60% 미만 <i style={{background:'#ed6e29'}}/>80% 미만 <i style={{background:'#d12b29'}}/>80% 이상</div>
  </section>;
}
export function OperatingPanel({snapshot}:{snapshot:Snapshot|null}){const o=snapshot?.operating;return <section className="dw-section"><h2>운전 변수 <span>{snapshot?timeLabel(snapshot.at):'기록 없음'}</span></h2><div className="dw-operating">{[
  ['3호기 출력',o?.plantMw,'MW'],['AMWI',o?.amwi,'무차원'],['급탄량',o?.feedTph,'t/h'],['저널 압력 실측',o?.journalActualPsi,'psi'],['보울 차압',o?.bowlDpMmH2o,'mmH₂O'],['분급기 속도',o?.classifierRpm,'rpm'],['모터 전류',o?.motorCurrentA,'A'],['저널 압력 설정',o?.journalSetPsi,'psi'],['분쇄 원단위',o?.specificEnergyKwhPerTon,'kWh/t']
].map(([label,v,unit])=><div key={String(label)}><span>{label}</span><strong>{fmt(v as number|undefined,2)}</strong><small>{unit}</small></div>)}</div><small>단위가 검증된 값만 표시합니다. B 원본의 모터 태그는 단위가 정의서와 달라 전류·전력으로 환산하지 않습니다. 분쇄 원단위는 전력량·처리량 검증 후 연결합니다.</small></section>;}
export function MaintenancePanel({record,at}:{record:MachineRecord|undefined;at:string}){const rows=record?.maintenance.filter(r=>Date.parse(r.at)<=Date.parse(at)).sort((a,b)=>Date.parse(b.at)-Date.parse(a.at))??[];return <section className="dw-section"><h2>정비 이력 <span>선택 시점까지 {rows.length}건</span></h2>{rows.length?<ol className="dw-history-list">{rows.map(r=><li key={r.id}><time>{date(r.at)}</time><b>{r.component} · {r.action}</b><p>{r.note}</p><small>{sourceLabel[r.source]}</small></li>)}</ol>:<Empty>선택 시점까지 확인된 정비 기록이 없습니다.<br/>정비 없음이 확인되었다는 의미는 아닙니다.</Empty>}</section>;}
