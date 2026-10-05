import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validateFrame, wearColor } from '../src/ce883/contract.ts';

// 테스트 전용 값. 운영 화면에 폴백으로 주입하지 않는다.
function fixture() {
  return {schemaVersion:1,machineId:'pulverizer_b',asOf:'2026-09-26T00:00:00Z',source:'scenario',displayMode:'scenario',running:false,bowlRpm:0,classifierRpm:0,wearLimitMm:38,
    rollers:[1,2,3].map(i=>({componentId:`tire_0${i}`,wearMm:Array.from({length:10},(_,z)=>z*5),valid:Array(10).fill(true)}))};
}
test('정상·0·한계 초과값을 왜곡 없이 보존',()=>{const f=validateFrame(fixture(),'pulverizer_b');assert.equal(f.rollers[0].wearMm[0],0);assert.equal(f.rollers[0].wearMm[9],45);});
test('B 데이터가 다른 설비로 유입되지 않음',()=>assert.throws(()=>validateFrame(fixture(),'pulverizer_a')));
test('롤 누락·중복 거부',()=>{const f=fixture();f.rollers[2].componentId='tire_01';assert.throws(()=>validateFrame(f,'pulverizer_b'));f.rollers.pop();assert.throws(()=>validateFrame(f,'pulverizer_b'));});
test('위치 수와 null, 음수, 비유한 수 검증',()=>{for(const bad of [null,-1,NaN,Infinity,'2']){const f=fixture();(f.rollers[0].wearMm as unknown[])[2]=bad;assert.throws(()=>validateFrame(f,'pulverizer_b'));}const f=fixture();f.rollers[0].valid.pop();assert.throws(()=>validateFrame(f,'pulverizer_b'));});
test('결측은 0 마모와 다른 색',()=>{const f=fixture();f.rollers[0].valid[0]=false;const v=validateFrame(f,'pulverizer_b');assert.equal(v.rollers[0].valid[0],false);assert.notEqual(wearColor(0,38,false),wearColor(0,38,true));});
test('잘못된 시간대·출처·모드·가동·한계 거부',()=>{for(const [key,value] of [['asOf','2026-09-26'],['source',''],['displayMode','x'],['running',undefined],['wearLimitMm',0],['bowlRpm',-2]]){const f={...fixture(),[key as string]:value};assert.throws(()=>validateFrame(f,'pulverizer_b'));}});
test('검증 결과는 원본 객체 변경에 영향받지 않음',()=>{const raw=fixture();const parsed=validateFrame(raw,'pulverizer_b');raw.rollers[0].wearMm[0]=999;assert.equal(parsed.rollers[0].wearMm[0],0);});
