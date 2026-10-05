import {fmt} from './depthFormat';
export interface LocalBPoint {raw:Record<string,string|number|null>}
const fields=[['plant_mw','3호기 출력','MW'],['motor_status','모터 가동 신호','1 기동 / 4 정지 / 그 외 확인 필요'],['motor_tag_raw','모터 태그 원값','단위 미확정'],['inlet_temp_c','입구 공기 온도','°C'],['outlet_temp_c','출구 온도','°C'],['bowl_dp_mmh2o','보울 차압','mmH₂O'],['classifier_demand_pct','분급기 속도 지령','%'],['classifier_speed_raw','분급기 속도 원값','단위 미확정'],['classifier_bias_raw','분급기 바이어스 원값','원본 단위 공란'],['tramp_valve_open','이물 배출 밸브 신호','원본 신호'],['hopper_temp_c','파이라이트 호퍼 온도','°C'],['journal_set_psi','저널 압력 설정','psi'],['journal_actual_psi','저널 압력 실측','psi'],['feed_tph','급탄량','t/h']];
export function LocalBPanel({point,at,busy,error,onTime,first,last}:{first:string;last:string;point:LocalBPoint|null;at:string;busy:boolean;error:string;onTime:(time:string)=>void}){
  return <section className="dw-section dw-local-b"><h2>B 원본 운전 기록 <span>1초 해상도</span></h2><p>{first?.slice(0,10)} — {last?.slice(0,10)} · 14개 태그</p><small>타임바는 5분 간격의 내보내기 값과 이벤트 시점입니다. 아래에서 원본의 특정 초를 직접 조회할 수 있습니다. 시간대는 한국 표준시(KST)로 해석했습니다.</small>
    <form onSubmit={e=>{e.preventDefault();const draft=new FormData(e.currentTarget).get('at');if(typeof draft==='string'&&draft)onTime(`${draft.length===16?draft+':00':draft}+09:00`);}}><label>원본 시각 (KST)<input name="at" aria-label="B 원본 조회 시각" type="datetime-local" step="1" min={first?.slice(0,19)} max={last?.slice(0,19)} required defaultValue={first?.slice(0,19)}/></label><button disabled={busy}>해당 초 조회</button></form>
    {error?<p role="alert">{error}</p>:<p aria-live="polite">{busy?'원본 조회 중…':`${at.slice(0,19).replace('T',' ')} KST`}</p>}
    <details open><summary>14개 태그 원값</summary><dl className="dw-def">{fields.map(([key,label,unit])=><div key={key} className="dw-raw-row"><dt>{label}<small>{unit}</small></dt><dd>{busy||!point?'—':fmt(point.raw[key] as number|null,3)}</dd></div>)}</dl></details>
    <small>모터 단위: 엑셀 CU.M/H ↔ 정의서 A로 불일치. 분급기 속도 단위는 엑셀에 없습니다. 원값을 보존하며, 회전수·전력으로 임의 변환하지 않습니다.</small>
  </section>;
}
