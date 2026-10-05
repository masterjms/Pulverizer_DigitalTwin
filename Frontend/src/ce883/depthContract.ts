import { validateFrame } from './contract.ts';
import type { MachineId, ComponentId, WearFrame } from './contract.ts';

export type Depth = 'exterior' | 'cutaway' | 'exploded';
export type RunStatus = 'running' | 'stopped' | 'fault' | 'unknown';
export type Provenance = 'measured' | 'model' | 'forecast' | 'scenario' | 'experiment';
export interface Operating {
  plantMw: number|null; motorCurrentA: number|null; feedTph: number|null;
  amwi: number|null; journalSetPsi: number|null; journalActualPsi: number|null;
  bowlDpMmH2o: number|null; classifierRpm: number|null; specificEnergyKwhPerTon: number|null;
}
export interface Reliability {
  wearMeanMm: number|null; wearRateUmPerHour: number|null; remainingHours: number|null;
  replacementAt: string|null; cycleOperatingHours: number|null; basis: string;
}
export interface Snapshot {
  at: string; source: Provenance; status: RunStatus; wear: WearFrame|null;
  operating: Operating; reliability: Reliability;
}
export interface MillEvent {
  id:string; at:string; title:string; kind:'operation'|'alarm'|'maintenance'|'replacement';
  source:Provenance; partId:ComponentId|null; zone:number|null;
  wearMm:number|null; beforeRateUmPerHour:number|null; afterRateUmPerHour:number|null;
  note:string;
  severity?:'high'|'medium'|'low'|null;
}
export interface LifePoint {at:string; remainingDays:number|null; source:Provenance; cycleId:string;}
export interface LifeCurve {componentId:ComponentId|null; zone:number|null; points:LifePoint[];}
export interface Maintenance {id:string; at:string; component:string; action:string; note:string; source:Provenance;}
export interface Correlation {coefficient:number; sampleCount:number; method:string; period:string; source:Provenance; note:string;}
export interface MachineRecord {
  machineId:MachineId; maxGapSeconds:number; snapshots:Snapshot[];
  events:MillEvent[]; maintenance:Maintenance[]; correlation:Correlation|null;
  lifeCurves?:LifeCurve[];
}
export interface DepthDataset {
  schemaVersion:2; title:string; generatedAt:string; scenario:boolean; baselineHours:number;
  machines:MachineRecord[];
  production:null|{date:string; asOf:string; targetMwh:number; actualMwh:number; source:Provenance; basis:string};
  costs:null|{period:string; asOf:string; baselineKrw:number; plannedKrw:number; source:Provenance; basis:string};
}
export const machineIds:MachineId[]=['pulverizer_a','pulverizer_b','pulverizer_c','pulverizer_d','pulverizer_e','pulverizer_f'];
export const statusLabels:Record<RunStatus,string>={running:'가동',stopped:'정지',fault:'상태 이상',unknown:'확인 불가'};
const sources=['measured','model','forecast','scenario','experiment'];
const numberOrNull=(v:unknown)=>v===null||(typeof v==='number'&&Number.isFinite(v)&&v>=0);
const time=(v:unknown):v is string=>typeof v==='string'&&/(Z|[+-]\d{2}:\d{2})$/.test(v)&&Number.isFinite(Date.parse(v));
const text=(v:unknown):v is string=>typeof v==='string'&&v.length<=4000;
function requireValue(ok:unknown,message:string):asserts ok {if(!ok)throw new Error(message);}
export function validateDataset(value:unknown):DepthDataset {
  requireValue(value&&typeof value==='object','기록 객체가 필요합니다.');
  const d=value as DepthDataset;
  requireValue(d.schemaVersion===2&&text(d.title)&&time(d.generatedAt)&&typeof d.scenario==='boolean','기록 버전·출처 정보가 올바르지 않습니다.');
  requireValue(typeof d.baselineHours==='number'&&Number.isFinite(d.baselineHours)&&d.baselineHours>0,'관행 교체시간이 필요합니다.');
  requireValue(Array.isArray(d.machines)&&d.machines.length<=6,'설비 기록은 A~F 범위여야 합니다.');
  const seen=new Set<string>();
  const acceptSource=(source:string)=>sources.includes(source)&&(d.scenario||source!=='scenario');
  for(const m of d.machines){
    requireValue(m&&machineIds.includes(m.machineId)&&!seen.has(m.machineId),'설비 ID 중복 또는 오류');seen.add(m.machineId);
    requireValue(Number.isFinite(m.maxGapSeconds)&&m.maxGapSeconds>0,'기록 유효 간격이 필요합니다.');
    requireValue(Array.isArray(m.snapshots)&&m.snapshots.length<=6000&&Array.isArray(m.events)&&m.events.length<=3000&&Array.isArray(m.maintenance),'기록 배열 오류');
    let previous=-Infinity;
    for(const s of m.snapshots){
      requireValue(s&&time(s.at)&&Date.parse(s.at)>previous&&acceptSource(s.source)&&['running','stopped','fault','unknown'].includes(s.status),'스냅샷은 출처를 포함하고 시각순으로 중복 없이 정렬해야 합니다.');previous=Date.parse(s.at);
      requireValue(s.wear===null||typeof s.wear==='object','마모 스냅샷 형식 오류');
      if(s.wear){validateFrame(s.wear,m.machineId);requireValue(Date.parse(s.wear.asOf)===Date.parse(s.at)&&s.wear.source===s.source,'마모와 기록의 시각·출처가 다릅니다.');requireValue(s.wear.running===(s.status==='running'),'마모 프레임의 가동 여부와 설비 상태가 다릅니다.');}
      requireValue(s.operating&&['plantMw','motorCurrentA','feedTph','amwi','journalSetPsi','journalActualPsi','bowlDpMmH2o','classifierRpm','specificEnergyKwhPerTon'].every(k=>numberOrNull(s.operating[k as keyof Operating])),'운전 변수는 유효한 값 또는 null이어야 합니다.');
      const r=s.reliability;
      requireValue(r&&['wearMeanMm','wearRateUmPerHour','remainingHours','cycleOperatingHours'].every(k=>numberOrNull(r[k as keyof Reliability]))&&text(r.basis)&&(r.replacementAt===null||time(r.replacementAt)),'교체 판단 값 또는 근거 오류');
    }
    const ids=new Set<string>();
    if(m.lifeCurves!==undefined){
      requireValue(Array.isArray(m.lifeCurves)&&m.lifeCurves.length<=34,'수명 곡선 배열 오류');
      const scopes=new Set<string>();
      for(const curve of m.lifeCurves){
        const scope=`${curve.componentId}:${curve.zone}`;
        requireValue(!scopes.has(scope)&&(curve.componentId===null||['tire_01','tire_02','tire_03'].includes(curve.componentId))&&(curve.zone===null||(curve.componentId!==null&&Number.isInteger(curve.zone)&&curve.zone>=1&&curve.zone<=10)),'수명 곡선 위치 오류');scopes.add(scope);
        requireValue(Array.isArray(curve.points)&&curve.points.length<=6000,'수명 곡선 크기 오류');let last=-Infinity;
        for(const p of curve.points){requireValue(time(p.at)&&Date.parse(p.at)>last&&numberOrNull(p.remainingDays)&&acceptSource(p.source)&&text(p.cycleId)&&p.cycleId.length>0,'잔여수명 시점·값·교체 주기 오류');last=Date.parse(p.at);}
      }
    }
    for(const e of m.events){
      requireValue(e&&text(e.id)&&!ids.has(e.id)&&time(e.at)&&text(e.title)&&text(e.note)&&acceptSource(e.source),'이벤트 식별자·시각·출처 오류');ids.add(e.id);
      requireValue(['operation','alarm','maintenance','replacement'].includes(e.kind)&&(e.partId===null||['tire_01','tire_02','tire_03'].includes(e.partId))&&(e.zone===null||(Number.isInteger(e.zone)&&e.zone>=1&&e.zone<=10&&e.partId!==null)),'이벤트 위치 오류');
      requireValue([e.wearMm,e.beforeRateUmPerHour,e.afterRateUmPerHour].every(numberOrNull),'이벤트 마모값 오류');
      requireValue(e.severity==null||['high','medium','low'].includes(e.severity),'이벤트 심각도 오류');
    }
    const maintenanceIds=new Set<string>();
    for(const r of m.maintenance){requireValue(r&&text(r.id)&&!maintenanceIds.has(r.id)&&time(r.at)&&text(r.component)&&text(r.action)&&text(r.note)&&acceptSource(r.source),'정비 기록 오류');maintenanceIds.add(r.id);}
    if(m.correlation!==null){const c=m.correlation;requireValue(c&&Number.isFinite(c.coefficient)&&Math.abs(c.coefficient)<=1&&Number.isInteger(c.sampleCount)&&c.sampleCount>=3&&text(c.method)&&text(c.period)&&text(c.note)&&acceptSource(c.source),'상관 분석 근거 오류');}
  }
  if(d.production!==null){const p=d.production;requireValue(p&&/^\d{4}-\d{2}-\d{2}$/.test(p.date)&&time(p.asOf)&&Number.isFinite(p.targetMwh)&&p.targetMwh>0&&typeof p.actualMwh==='number'&&numberOrNull(p.actualMwh)&&acceptSource(p.source)&&text(p.basis),'발전 목표·실적·근거 오류');}
  if(d.costs!==null){const c=d.costs;requireValue(c&&text(c.period)&&time(c.asOf)&&typeof c.baselineKrw==='number'&&typeof c.plannedKrw==='number'&&[c.baselineKrw,c.plannedKrw].every(numberOrNull)&&acceptSource(c.source)&&text(c.basis),'비용 비교·근거 오류');}
  return structuredClone(d);
}
export function snapshotAt(record:MachineRecord|undefined,at:string):Snapshot|null {
  if(!record||!time(at))return null;
  const t=Date.parse(at);let lo=0,hi=record.snapshots.length-1,index=-1;
  while(lo<=hi){const mid=(lo+hi)>>1;if(Date.parse(record.snapshots[mid].at)<=t){index=mid;lo=mid+1;}else hi=mid-1;}
  const s=record.snapshots[index];return s&&t-Date.parse(s.at)<=record.maxGapSeconds*1000?s:null;
}
export function historyTimes(d:DepthDataset|null,machine:MachineId):string[]{return d?.machines.find(m=>m.machineId===machine)?.snapshots.map(s=>s.at)??[];}
export function productionPercent(p:DepthDataset['production']):number|null{return p?p.actualMwh/p.targetMwh*100:null;}
export function costDifference(c:DepthDataset['costs']):number|null{return c?c.baselineKrw-c.plannedKrw:null;}
