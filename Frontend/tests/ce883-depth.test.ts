import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {validateDataset,snapshotAt,productionPercent,costDifference} from '../src/ce883/depthContract.ts';
const fixture=()=>JSON.parse(readFileSync(new URL('../public/ce883-data/TEST_ONLY_depth_example.json',import.meta.url),'utf8'));
test('가상 예제는 명시된 출처로만 수용하고 원본 변경과 격리',()=>{const raw=fixture();const data=validateDataset(raw);raw.machines[1].snapshots[0].reliability.remainingHours=0;assert.equal(data.machines[1].snapshots[0].reliability.remainingHours,7000);raw.scenario=false;assert.throws(()=>validateDataset(raw));});
test('기준 시각 이전의 기록만 읽으며 미래값과 오래된 값은 반환하지 않음',()=>{const b=validateDataset(fixture()).machines[1];assert.equal(snapshotAt(b,'2025-04-30T00:00:00Z'),null);assert.equal(snapshotAt(b,'2025-05-02T00:00:00Z')?.at,'2025-05-02T09:00:00+09:00');assert.equal(snapshotAt(b,new Date(Date.parse(b.snapshots[b.snapshots.length-1].at)+86401000).toISOString()),null);});
test('설비가 없거나 상태가 unknown이면 가동으로 바꾸지 않음',()=>{const d=validateDataset(fixture());assert.equal(snapshotAt(undefined,d.generatedAt),null);assert.equal(snapshotAt(d.machines[5],d.generatedAt)?.status,'unknown');assert.equal(snapshotAt(d.machines[0],d.generatedAt)?.wear,null);});
test('스냅샷 시각·출처·설비가 섞인 입력은 거부',()=>{for(const change of ['machine','time','source']){const d=fixture();const s=d.machines[1].snapshots[0];if(change==='machine')s.wear.machineId='pulverizer_a';if(change==='time')s.wear.asOf='2025-01-01T00:00:00Z';if(change==='source')s.wear.source='measured';assert.throws(()=>validateDataset(d));}});
test('중복 설비와 역순 기록을 거부',()=>{const d=fixture();d.machines[1].machineId=d.machines[0].machineId;assert.throws(()=>validateDataset(d));const second=fixture();second.machines[1].snapshots.reverse();assert.throws(()=>validateDataset(second));});

test('Unity 운전 상태와 React 표시 상태가 충돌하는 입력을 거부',()=>{const d=fixture();d.machines[1].snapshots[0].status='stopped';assert.throws(()=>validateDataset(d));});
test('생산 목표가 0이면 거부하고 실제 초과 달성률은 보존',()=>{const d=fixture();d.production.targetMwh=0;assert.throws(()=>validateDataset(d));const p=fixture().production;p.actualMwh=15000;assert.equal(productionPercent(p),125);assert.equal(productionPercent(null),null);});
test('비용 상승을 절감으로 바꾸지 않고 결측은 미산정 유지',()=>{const c=fixture().costs;c.plannedKrw=80000000;assert.equal(costDifference(c),-8000000);assert.equal(costDifference(null),null);});
test('잘못된 센서와 상관계수, 범위 밖 이벤트 위치 입력은 거부',()=>{const d=fixture();d.machines[1].snapshots[0].operating.journalActualPsi=-1;assert.throws(()=>validateDataset(d));const bad=fixture();bad.machines[1].events[0].zone=11;assert.throws(()=>validateDataset(bad));const correlation=fixture();correlation.machines[1].correlation={coefficient:1.1,sampleCount:30,method:'Pearson',period:'2025-05',source:'scenario',note:'invalid coefficient'};assert.throws(()=>validateDataset(correlation));});
