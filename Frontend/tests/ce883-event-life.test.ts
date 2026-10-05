import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {lifePaths,lifeSeries,lifeValueAt,orderEvents,relevantEvent} from '../src/ce883/eventLife.ts';
import {validateDataset} from '../src/ce883/depthContract.ts';
import type {LifePoint,MachineRecord,MillEvent} from '../src/ce883/depthContract.ts';
const stamp=(day:number)=>new Date(Date.UTC(2026,0,1)+day*86400000).toISOString();
const point=(day:number,value:number|null,cycle='one',source:LifePoint['source']='model'):LifePoint=>({at:stamp(day),remainingDays:value,cycleId:cycle,source});
const fixture=()=>validateDataset(JSON.parse(readFileSync(new URL('../public/ce883-data/TEST_ONLY_depth_example.json',import.meta.url),'utf8')));
test('이벤트 보간은 교체·결측·기간 밖을 넘지 않음',()=>{
  const ps=[point(0,100),point(1,80),point(2,180,'two'),point(3,null,'two'),point(4,140,'two'),point(7,80,'two'),point(8,0,'two')];
  assert.equal(lifeValueAt(ps,stamp(.5),86400),90);
  for(const day of [-1,1.5,2.5,3.5,5,9])assert.equal(lifeValueAt(ps,stamp(day),86400),null);
  assert.equal(lifeValueAt(ps,stamp(8),86400),0);
  const paths=lifePaths(ps,86400,t=>Date.parse(t),v=>v);
  assert.ok(!paths.history.includes(`M${Date.parse(stamp(1))},80 L${Date.parse(stamp(2))},180`));
  assert.ok(!paths.history.includes(`M${Date.parse(stamp(4))},140 L${Date.parse(stamp(7))},80`));
});
test('예측 점선은 같은 주기의 마지막 이력점에서 시작',()=>{
  const paths=lifePaths([point(0,20),point(1,10,'one','forecast'),point(2,0,'one','forecast')],86400,t=>Date.parse(t),v=>v);
  assert.ok(paths.forecast.startsWith(`M${Date.parse(stamp(0))},20 L${Date.parse(stamp(1))},10`));
});
test('부품 수명 미연결은 설비 기준으로 명시하고 운전시간을 일수로 변환하지 않음',()=>{
  const r=fixture().machines[1];assert.equal(lifeSeries(r,{partId:'tire_01',zone:3}).label,'설비 교체 기준');
  const raw={...r,lifeCurves:undefined,snapshots:r.snapshots.map(s=>({...s,reliability:{...s.reliability,replacementAt:null,remainingHours:1000}}))};
  assert.ok(lifeSeries(raw,null).points.every(p=>p.remainingDays===null));
});
test('미분류 심각도는 마지막, 부품 필터는 설비 공통 이벤트 보존',()=>{
  const base=fixture().machines[1].events[0];
  const events=[{...base,id:'none',severity:null},{...base,id:'low',severity:'low'},{...base,id:'high',severity:'high'}] as MillEvent[];
  assert.deepEqual(orderEvents(events,'severity').map(e=>e.id),['high','low','none']);
  assert.equal(relevantEvent({...base,partId:null},{partId:'tire_01',zone:4}),true);
  assert.equal(relevantEvent({...base,partId:'tire_02'},{partId:'tire_01',zone:4}),false);
});
test('음수 잔여수명·중복 시각·잘못된 심각도 거부',()=>{
  const mutate=(fn:(r:MachineRecord)=>void)=>{const d=fixture();fn(d.machines[1]);assert.throws(()=>validateDataset(d));};
  mutate(r=>{r.lifeCurves![0].points[0].remainingDays=-1;});
  mutate(r=>{r.lifeCurves![0].points[1].at=r.lifeCurves![0].points[0].at;});
  mutate(r=>{(r.events[0] as unknown as {severity:string}).severity='critical';});
});
