import type {ComponentId} from './contract.ts';
import type {LifePoint,MachineRecord,MillEvent} from './depthContract.ts';
export type PartSelection={partId:ComponentId;zone:number}|null;
export const severityLabel={high:'상',medium:'중',low:'하'};
export function relevantEvent(e:MillEvent,selection:PartSelection){return !selection||e.partId===null||(e.partId===selection.partId&&(e.zone===null||!selection.zone||e.zone===selection.zone));}
export function lifeSeries(record:MachineRecord|undefined,selection:PartSelection):{points:LifePoint[];label:string}{
  if(!record)return {points:[],label:'설비 교체 기준'};
  const exact=record.lifeCurves?.find(c=>c.componentId===(selection?.partId??null)&&c.zone===(selection?.zone||null));
  if(exact)return {points:exact.points,label:selection?`롤 ${Number(selection.partId.slice(-2))}${selection.zone?` · 위치 ${selection.zone}`:''}`:'설비 교체 기준'};
  const shared=record.lifeCurves?.find(c=>c.componentId===null&&c.zone===null);
  if(shared)return {points:shared.points,label:'설비 교체 기준'};
  // Calendar days derive only from the supplied replacement date, never operating hours / 24.
  const replacements=record.events.filter(e=>e.kind==='replacement'&&e.partId===null).sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));
  return {label:'설비 교체 기준',points:record.snapshots.map(s=>({at:s.at,remainingDays:s.reliability.replacementAt?Math.max(0,(Date.parse(s.reliability.replacementAt)-Date.parse(s.at))/86400000):null,source:s.source,cycleId:replacements.filter(e=>Date.parse(e.at)<=Date.parse(s.at)).slice(-1)[0]?.id??'initial'}))};
}
export function lifeValueAt(points:LifePoint[],at:string,maxGapSeconds:number):number|null{
  const t=Date.parse(at);let lo=0,hi=points.length-1;
  while(lo<=hi){const mid=(lo+hi)>>1;if(Date.parse(points[mid].at)<t)lo=mid+1;else hi=mid-1;}
  const b=points[lo],a=points[lo-1];
  if(b&&Date.parse(b.at)===t)return b.remainingDays;
  if(!a||!b||a.remainingDays===null||b.remainingDays===null||a.cycleId!==b.cycleId)return null;
  const span=Date.parse(b.at)-Date.parse(a.at);if(span>maxGapSeconds*1000)return null;
  return a.remainingDays+(b.remainingDays-a.remainingDays)*(t-Date.parse(a.at))/span;
}
export function lifePaths(points:LifePoint[],maxGapSeconds:number,x:(at:string)=>number,y:(v:number)=>number){
  let history='',forecast='';let prior:LifePoint|null=null;
  for(const p of points){if(p.remainingDays===null){prior=null;continue;}
    const join=prior&&prior.remainingDays!==null&&prior.cycleId===p.cycleId&&Date.parse(p.at)-Date.parse(prior.at)<=maxGapSeconds*1000;
    const segment=join?`M${x(prior!.at)},${y(prior!.remainingDays!)} L${x(p.at)},${y(p.remainingDays)} `:`M${x(p.at)},${y(p.remainingDays)} `;
    if(p.source==='forecast')forecast+=segment;else history+=segment;prior=p;
  }return {history,forecast};
}
export function orderEvents(events:MillEvent[],sort:'latest'|'severity'){
  const rank={high:3,medium:2,low:1};return [...events].sort((a,b)=>(sort==='severity'?(rank[b.severity??'low']*(b.severity?1:0)-rank[a.severity??'low']*(a.severity?1:0)):0)||Date.parse(b.at)-Date.parse(a.at));
}
