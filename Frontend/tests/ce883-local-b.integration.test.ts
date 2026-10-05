// Explicit integration test: requires the verified B API and Vite local proxy.
import test from 'node:test';
import assert from 'node:assert/strict';
import {validateDataset} from '../src/ce883/depthContract.ts';
const base='http://127.0.0.1:5173/api/local-b';
test('로컬 B API → Vite → 화면 계약, 실제 기록과 결측 보존',async()=>{
  const response=await fetch(`${base}/workspace`);assert.equal(response.status,200);
  const text=await response.text();assert.ok(Buffer.byteLength(text)<5_000_000);
  const data=validateDataset(JSON.parse(text));assert.equal(data.scenario,false);
  assert.equal(data.machines.length,1);assert.equal(data.machines[0].machineId,'pulverizer_b');
  assert.equal(data.machines[0].snapshots.length,2881);
  assert.equal(data.machines[0].snapshots[0].operating.feedTph,38.10734558105469);
  assert.equal(data.machines[0].snapshots[0].wear,null);
});
test('1초 직접 조회 및 기간 밖 시각의 404',async()=>{
  const response=await fetch(`${base}/point?at=${encodeURIComponent('2025-12-10T00:00:01+09:00')}`);
  const point=await response.json();assert.equal(point.raw.plant_mw,514.9482421875);
  assert.equal(point.snapshot.at,'2025-12-10T00:00:01+09:00');
  const missing=await fetch(`${base}/point?at=${encodeURIComponent('2025-12-20T00:00:00+09:00')}`);
  assert.equal(missing.status,404);
});
test('원본 전체 검증 지문, 행 수와 데이터베이스의 읽기 전용 상태',async()=>{
  const audit=await (await fetch(`${base}/audit`)).json();
  assert.equal(audit.rows,864000);assert.equal(audit.verifiedNumericCells,12096000);
  assert.equal(audit.sourceRowFingerprint,audit.cacheRowFingerprint);
  const health=await (await fetch(`${base}/health`)).json();assert.equal(health.readOnly,true);
});

test('3월 검사 기록은 실제 운전 시각과 연결되고 잔여수명은 미산정으로 보존',async()=>{
  const response=await fetch(`${base}/march/workspace`);assert.equal(response.status,200);
  const data=validateDataset(await response.json());const record=data.machines[0];
  assert.equal(data.scenario,false);assert.equal(record.snapshots.length,3460);
  assert.equal(record.events.length,4);assert.equal(record.maintenance.length,1);
  for(const event of record.events){
    assert.equal(event.severity,null);
    assert.ok(record.snapshots.some(s=>s.at===event.at));
  }
  assert.ok(record.snapshots.every(s=>s.wear===null&&s.reliability.replacementAt===null));
  for(const [time,code,status] of [['08:45:24',4,'stopped'],['10:11:41',20,'fault'],['15:36:52',1,'running']] as const){
    const at=`2026-03-27T${time}+09:00`;
    const point=await (await fetch(`${base}/march/point?at=${encodeURIComponent(at)}`)).json();
    assert.equal(point.raw.motor_status,code);assert.equal(point.snapshot.status,status);
  }
  const noon=await (await fetch(`${base}/march/point?at=${encodeURIComponent('2026-03-27T12:00:00+09:00')}`)).json();
  assert.ok(noon.raw.feed_tph<0);assert.equal(noon.snapshot.operating.feedTph,null);
  const audit=await (await fetch(`${base}/march/audit`)).json();
  assert.equal(audit.rows,1036800);assert.equal(audit.sourceRowFingerprint,audit.cacheRowFingerprint);
});
