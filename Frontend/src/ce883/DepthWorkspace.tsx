import { useEffect,useMemo,useRef,useState } from 'react';
import type { MachineId,ComponentId,WearFrame } from './contract';
import { sourceLabel,validateFrame } from './contract';
import { historyTimes,machineIds,snapshotAt,statusLabels,validateDataset } from './depthContract';
import type { Depth,DepthDataset,MillEvent,Operating,Reliability,Snapshot } from './depthContract';
import {LocalBPanel} from './LocalBPanel';
import type {LocalBPoint} from './LocalBPanel';
import { useDepthUnity } from './useDepthUnity';
import {EventLifePanel,EventLifeDetails} from './EventLifePanel';
import { Executive,MaintenancePanel,OperatingPanel,Replacement,ZoneMatrix } from './DepthPanels';
import {date,timeLabel} from './depthFormat';
import './depth.css';
import './fieldDesktop.css';
const stages:{id:Depth;name:string;intent:string}[]=[{id:'exterior',name:'외형',intent:'운영 총괄'},{id:'cutaway',name:'절개',intent:'교체 계획'},{id:'exploded',name:'분해',intent:'현장 분석'}];
const noOperating:Operating={plantMw:null,motorCurrentA:null,feedTph:null,amwi:null,journalSetPsi:null,journalActualPsi:null,bowlDpMmH2o:null,classifierRpm:null,specificEnergyKwhPerTon:null};
const noReliability:Reliability={wearMeanMm:null,wearRateUmPerHour:null,remainingHours:null,replacementAt:null,cycleOperatingHours:null,basis:''};
function oneFrame(frame:WearFrame):DepthDataset{return {schemaVersion:2,title:'단일 마모 스냅샷',generatedAt:frame.asOf,scenario:frame.source==='scenario',baselineHours:20000,machines:[{machineId:frame.machineId,maxGapSeconds:1,snapshots:[{at:frame.asOf,source:frame.source,status:frame.running?'running':'stopped',wear:frame,operating:noOperating,reliability:noReliability}],events:[],maintenance:[],correlation:null}],production:null,costs:null};}
export function DepthWorkspace(){
  const canvas=useRef<HTMLCanvasElement>(null);const pointer=useRef<{id:number;x:number;y:number;moved:boolean}|null>(null);
  const [depth,setDepth]=useState<Depth>('exterior');const [machine,setMachine]=useState<MachineId>('pulverizer_b');
  const [selection,setSelection]=useState<{partId:ComponentId;zone:number}|null>(null);
  const [data,setData]=useState<DepthDataset|null>(null);const [at,setAt]=useState(()=>new Date().toISOString());
  const [playing,setPlaying]=useState(false);const [eventId,setEventId]=useState<string|null>(null);
  const [error,setError]=useState('');const [busy,setBusy]=useState(false);const [connectionOpen,setConnectionOpen]=useState(false);const [endpoint,setEndpoint]=useState('');
  const [localB,setLocalB]=useState(false);const [localBSource,setLocalBSource]=useState<'march'|'december'>('march');
  const localBApi=localBSource==='march'?'/api/local-b/march':'/api/local-b';const [localBRevision,setLocalBRevision]=useState(0);const [point,setPoint]=useState<(LocalBPoint&{snapshot:Snapshot})|null>(null);const [pointBusy,setPointBusy]=useState(false);const [pointError,setPointError]=useState('');
  const request=useRef(0);const mounted=useRef(true);
  useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;};},[]);
  useEffect(()=>{if(depth==='exploded'&&!selection)setSelection({partId:'tire_01',zone:0});},[depth,selection]);
  const record=data?.machines.find(m=>m.machineId===machine);
  const snapshot=useMemo(()=>localB&&machine==='pulverizer_b'&&point?.snapshot.at===at?point.snapshot:snapshotAt(record,at),[localB,machine,point,record,at]);
  useEffect(()=>{
    if(!localB||machine!=='pulverizer_b')return;
    const controller=new AbortController();setPointBusy(true);setPoint(null);setPointError('');
    void fetch(`${localBApi}/point?at=${encodeURIComponent(at)}`,{signal:controller.signal}).then(async response=>{
      if(!response.ok)throw new Error('해당 초의 원본 기록을 조회하지 못했습니다.');
      const value=await response.json() as LocalBPoint&{snapshot:Snapshot};
      if(!controller.signal.aborted)setPoint(value);
    }).catch(e=>{if(!controller.signal.aborted)setPointError(e instanceof Error?e.message:'원본 조회 실패');}).finally(()=>{if(!controller.signal.aborted)setPointBusy(false);});
    return()=>controller.abort();
  },[localB,localBRevision,localBApi,machine,at]);
  const times=useMemo(()=>historyTimes(data,machine),[data,machine]);
  const index=times.reduce((last,t,i)=>Date.parse(t)<=Date.parse(at)?i:last,0);
  const fleet=useMemo(()=>machineIds.map(id=>({machineId:id,status:(id===machine?snapshot:snapshotAt(data?.machines.find(m=>m.machineId===id),at))?.status??'unknown' as const})),[data,at,machine,snapshot]);
  const view=useMemo(()=>({machine,depth,selection,at,frame:snapshot?.wear??null,fleet}),[machine,depth,selection,at,snapshot,fleet]);
  function chooseMachine(id:MachineId){setMachine(id);setDepth('cutaway');setSelection(null);setEventId(null);setPlaying(false);setError('');}
  function choosePart(partId:ComponentId,zone:number){setDepth('exploded');setSelection({partId,zone});}
  function stage(next:Depth){setDepth(next);setSelection(null);setEventId(null);setPlaying(false);}
  const unity=useDepthUnity(canvas,view,notice=>{
    if(notice.type==='machinePicked'&&notice.machineId)chooseMachine(notice.machineId);
    if(notice.type==='partSelected'&&notice.machineId===machine&&notice.partId)choosePart(notice.partId,notice.zone??0);
    if(notice.type==='selectionCleared')stage('exterior');
    if(notice.type==='error')setError(notice.message??'모델 수신 오류');
  });
  useEffect(()=>{
    if(!playing||times.length<2)return;
    const timer=window.setInterval(()=>{const next=times.find(t=>Date.parse(t)>Date.parse(at));if(next)setAt(next);else setPlaying(false);setEventId(null);},800);
    return()=>window.clearInterval(timer);
  },[playing,times,at]);
  function cursor(value:string){setAt(value);setPlaying(false);setEventId(null);}
  function pickEvent(e:MillEvent){setAt(e.at);setPlaying(false);setEventId(e.id);if(e.partId)choosePart(e.partId,e.zone??0);}
  function install(value:unknown){
    let next:DepthDataset;
    if((value as {schemaVersion?:number})?.schemaVersion===1){const f=validateFrame(value,(value as WearFrame).machineId);if(!machineIds.includes(f.machineId))throw new Error('설비 식별자 오류');next=validateDataset(oneFrame(f));}
    else next=validateDataset(value);
    setData(next);setLocalB(false);setPoint(null);setPlaying(false);setSelection(null);setEventId(null);setError('');
    const allTimes=next.machines.flatMap(m=>m.snapshots.map(s=>s.at)).sort((a,b)=>Date.parse(a)-Date.parse(b));const last=allTimes[allTimes.length-1];
    setAt(last??next.generatedAt);
    if(!next.machines.some(m=>m.machineId===machine)&&next.machines.length)setMachine(next.machines[0].machineId);
  }
  async function importFile(file?:File){if(!file)return;const token=++request.current;setBusy(true);try{if(file.size>5_000_000)throw new Error('기록 파일은 5 MB 이하여야 합니다.');const value=JSON.parse(await file.text());if(token===request.current&&mounted.current)install(value);}catch(e){if(token===request.current&&mounted.current)setError(e instanceof Error?e.message:'파일 읽기 실패');}finally{if(token===request.current&&mounted.current)setBusy(false);}}
  async function loadUrl(path:string,nextDepth?:Depth){const token=++request.current;setBusy(true);try{const url=new URL(path,location.origin);if(url.origin!==location.origin)throw new Error('동일 출처의 읽기 API만 연결할 수 있습니다.');const response=await fetch(url,{signal:AbortSignal.timeout(15000)});if(!response.ok)throw new Error(`기록 조회 실패 (${response.status})`);const body=await response.text();if(body.length>5_000_000)throw new Error('기록이 5 MB를 초과합니다.');if(token===request.current&&mounted.current){install(JSON.parse(body));if(nextDepth)setDepth(nextDepth);}}catch(e){if(token===request.current&&mounted.current)setError(e instanceof Error?e.message:'조회 실패');}finally{if(token===request.current&&mounted.current)setBusy(false);}}
  async function connectLocalB(source:'march'|'december'='march'){const token=++request.current;setBusy(true);try{const response=await fetch(source==='march'?'/api/local-b/march/workspace':'/api/local-b/workspace',{signal:AbortSignal.timeout(15000)});if(!response.ok)throw new Error('로컬 B 서버에 연결할 수 없습니다. Start-Ce883Local.ps1을 실행해 주세요.');const value=await response.json();if(token===request.current&&mounted.current){install(value);setLocalBSource(source);setLocalB(true);setLocalBRevision(n=>n+1);setMachine('pulverizer_b');setDepth(source==='march'?'exploded':'cutaway');setConnectionOpen(false);}}catch(e){if(token===request.current&&mounted.current)setError(e instanceof Error?e.message:'B 연결 실패');}finally{if(token===request.current&&mounted.current)setBusy(false);}}
  function clear(){request.current++;setBusy(false);setPlaying(false);setData(null);setLocalB(false);setPoint(null);setAt(new Date().toISOString());setSelection(null);setEventId(null);setError('');}
  const status=snapshot?.status??'unknown';
  const missingAt=record&&record.snapshots.length>0&&!snapshot;
  return <main className={`dw-app ${depth==='exploded'?'dw-field-mode':''}`}>
    <header className="dw-header"><a href={import.meta.env.BASE_URL}>nextro<small>설비 디지털 트윈</small></a><div className="dw-heading"><h1>보령 3호기 / 미분기 {depth==='exterior'?'A–F':machine.slice(-1).toUpperCase()}</h1><p>{depth==='exterior'?'운영의 전체 그림에서 설비와 부품까지':selection?`롤 ${Number(selection.partId.slice(-2))}${selection.zone?` / 위치 ${selection.zone}`:''} 상세`:'CE 883HP · 상태와 교체 판단'}</p></div><div className="dw-header-tools"><label className="dw-file">기록 불러오기<input type="file" accept="application/json,.json" disabled={busy} onChange={e=>{void importFile(e.target.files?.[0]);e.target.value='';}}/></label><button onClick={()=>setConnectionOpen(!connectionOpen)}>데이터 연결</button></div></header>
    <nav className="dw-depth" aria-label="정보 깊이">{stages.map((s,i)=><button key={s.id} aria-pressed={depth===s.id} className={depth===s.id?'active':''} onClick={()=>stage(s.id)}><b>{i+1}</b><strong>{s.name}</strong><span>{s.intent}</span></button>)}<div className="dw-time-context"><strong>{data?.scenario?'가상 시나리오':data?(snapshot?.source==='forecast'?'예측 기록':'과거 기록'):'데이터 미연결'}</strong><time>{data?`${date(at)} ${timeLabel(at).split(' ').slice(-1)[0]} KST`:'실제 운전값 연결 대기'}</time></div></nav>
    <section className="dw-local-status"><div><b>{localB?'B 실측 기록 연결됨':'B 실측 데이터로 확인하기'}</b><span>{localB?`${data?.title} · A·C·D·E·F는 데이터 없음`:'3월 27일 검사 이벤트와 당시 운전 기록을 확인합니다.'}</span></div><button disabled={busy} onClick={()=>void connectLocalB()}>{busy?'연결 중…':localB?'3월 검사 기록 다시 연결':'3월 검사 기록 연결'}</button><button disabled={busy} onClick={()=>void connectLocalB('december')}>12월 기록</button><button disabled={busy} onClick={()=>void loadUrl(`${import.meta.env.BASE_URL}ce883-data/TEST_ONLY_depth_example.json`,'exploded')}>가상 그래프 예제</button>{localB&&<button onClick={clear}>연결 해제</button>}</section>
    {data?.scenario&&<div className="dw-scenario" role="status">화면 검토용 가상 데이터입니다. 실제 설비 상태·마모·교체일·비용·생산 실적이 아닙니다.<button onClick={clear}>예제 닫기</button></div>}
    {connectionOpen&&<section className="dw-connection"><div><b>검증된 기록 연결</b><p>스냅샷·이벤트·정비 기록을 포함한 v2 JSON을 불러오거나 동일 출처 API를 입력하세요.</p><input aria-label="기록 API 경로" placeholder="/api/v2/twin/workspace" value={endpoint} onChange={e=>setEndpoint(e.target.value)}/><button disabled={!endpoint||busy} onClick={()=>void loadUrl(endpoint)}>조회하여 적용</button></div><div><b>동작만 먼저 살펴보기</b><p>별도로 표시되는 가상 기록으로 타임바와 마모색·이벤트 연동을 확인합니다.</p><button disabled={busy} onClick={()=>void loadUrl(`${import.meta.env.BASE_URL}ce883-data/TEST_ONLY_depth_example.json`)}>가상 UI 예제 열기</button>{data&&<button onClick={clear}>연결 해제</button>}</div></section>}
    {error&&<div className="dw-error" role="alert">{error}<button onClick={()=>setError('')}>닫기</button></div>}
    <div className={`dw-main ${depth}`}>
      {depth==='exploded'&&<EventLifePanel key={`${data?.title}-${machine}`} record={record} at={at} selection={selection} selectedEvent={eventId} onEvent={pickEvent} snapshot={snapshot}/>}
      <div className="dw-model-column"><section className="dw-viewer" aria-label="Unity 미분기 모델"><div className="dw-camera-tools"><button onClick={()=>unity.send('SetCamera','iso')}>기본 시점</button><button onClick={()=>unity.send('SetCamera','front')}>정면</button><button onClick={()=>unity.send('SetCamera','top')}>평면</button>{selection&&depth!=='exploded'&&<button onClick={()=>setSelection(null)}>분해 전체로</button>}</div>
        <div className="dw-viewer-label"><span>{depth==='exterior'?'미분기 A–F':depth==='exploded'?`롤타이어 ${Number(selection?.partId.slice(-2)??1)}`:`미분기 ${machine.slice(-1).toUpperCase()}`}</span><small>{depth==='exterior'?'설비 선택 → 절개 · 교체 계획':depth==='cutaway'?'롤 선택 → 분해 · 현장 분석':selection?'선택 부품 확대':'부품을 선택해 위치별 상태 확인'}</small></div>
        <canvas ref={canvas} tabIndex={0} id="ce883-unity-canvas" aria-label="미분기 모델. 드래그 회전, 휠 확대, 설비와 롤 클릭"
          onPointerDownCapture={e=>{pointer.current=e.button===0?{id:e.pointerId,x:e.clientX,y:e.clientY,moved:false}:null;}}
          onPointerMoveCapture={e=>{const p=pointer.current;if(p&&p.id===e.pointerId&&Math.hypot(e.clientX-p.x,e.clientY-p.y)>5)p.moved=true;}}
          onPointerCancelCapture={()=>{pointer.current=null;}}
          onPointerUpCapture={e=>{const p=pointer.current;pointer.current=null;if(!p||p.id!==e.pointerId||p.moved||Math.hypot(e.clientX-p.x,e.clientY-p.y)>5)return;const r=e.currentTarget.getBoundingClientRect();unity.send('PickViewport',JSON.stringify({x:(e.clientX-r.left)/r.width,y:1-(e.clientY-r.top)/r.height}));}}/>
        {(unity.loading||unity.error)&&<div className="dw-loading">{unity.error||unity.loading}</div>}
        <footer>{depth==='exterior'?'6대 외형 · 상태등은 수신한 가동 상태만 표시':'드래그 회전 · 휠 확대 · Escape 전체 보기'}<span>{snapshot?sourceLabel[snapshot.source]:'상태 미확인'}</span></footer>
      {depth==='exploded'&&<div className="dw-part-switch" aria-label="확대할 롤 선택">{(['tire_01','tire_02','tire_03'] as ComponentId[]).map((id,i)=><button key={id} aria-pressed={selection?.partId===id} onClick={()=>choosePart(id,0)}>롤 {i+1}</button>)}</div>}
      </section>
      {depth==='exploded'&&<section className="dw-future-space" aria-label="미래 시뮬레이션 검토 영역"><div><small>미래 시뮬레이션</small><h2>운전 조건을 바꾸면, 수명은 어떻게 달라질까?</h2><p>이 영역은 시뮬레이션 화면을 검토 중입니다.</p><span>현재는 예측 계산을 실행하지 않습니다.</span></div></section>}
      <div className="dw-fleet-strip">{fleet.map(m=><button key={m.machineId} className={depth!=='exterior'&&m.machineId===machine?'selected':''} onClick={()=>chooseMachine(m.machineId)}><b>{m.machineId.slice(-1).toUpperCase()}</b><span className={`dw-status ${m.status}`}>{statusLabels[m.status]}</span></button>)}</div>
      {depth!=='exterior'&&<section className="dw-timeline" aria-label="과거 기록 재생"><div className="dw-timeline-top"><button disabled={times.length<2} aria-label={playing?'과거 재생 일시정지':'과거 기록 재생'} onClick={()=>{if(!playing&&index>=times.length-1)setAt(times[0]);setPlaying(!playing);}}>{playing?'Ⅱ':'▶'}</button><div><b>과거 기록 재생</b><small>{times.length?`${date(times[0])} — ${date(times[times.length-1])}`:'선택 설비의 시계열 기록이 필요합니다.'}</small></div><time>{data?timeLabel(at):'—'}</time><button disabled={!times.length} onClick={()=>cursor(times[times.length-1])}>마지막 기록</button></div>
        <div className="dw-timeline-track">{record?.events.map(e=>{const first=Date.parse(times[0]),last=Date.parse(times[times.length-1]);const left=(Date.parse(e.at)-first)/Math.max(1,last-first)*100;return left>=0&&left<=100?<button key={e.id} style={{left:`${left}%`}} title={`${timeLabel(e.at)} ${e.title}`} aria-label={e.title} className={e.kind} onClick={()=>pickEvent(e)}/>:null;})}</div>
        <input type="range" aria-label="과거 시점" min="0" max={Math.max(0,times.length-1)} value={index} disabled={times.length<2} onChange={e=>cursor(times[Number(e.target.value)])}/><small>기록된 상태를 시간순으로 재생합니다. 운전 조건을 바꾸는 미래 예측과 구분됩니다.</small>
      </section>}
            {depth==='cutaway'&&<MaintenancePanel record={record} at={at}/>}
    </div>
    <aside className="dw-panel" aria-label="설비 상세 정보" tabIndex={depth==='exploded'?0:undefined}>{depth==='exterior'?<Executive data={data} at={at}/>:<>
      <section className="dw-machine-summary"><div><small>선택 설비</small><h2>미분기 {machine.slice(-1).toUpperCase()}</h2></div><span className={`dw-status ${status}`}>{statusLabels[status]}</span><p>{snapshot?`${timeLabel(snapshot.at)} KST · ${sourceLabel[snapshot.source]}`:missingAt?'선택 시각의 유효 기록 없음 · 이전 값을 현재 상태로 표시하지 않습니다.':'선택 설비의 검증된 상태를 연결하세요.'}</p></section>
      {depth==='exploded'&&<EventLifeDetails record={record} selectedEvent={eventId} snapshot={snapshot}/>}
      <Replacement snapshot={snapshot} baseline={data?.baselineHours??20000} at={at}/>
      {localB&&machine==='pulverizer_b'&&<LocalBPanel key={localBSource} first={times[0]} last={times[times.length-1]} point={point?.snapshot.at===at?point:null} at={at} busy={pointBusy} error={pointError} onTime={cursor}/>}
      {depth==='cutaway'?<section className="dw-section"><h2>부품 상태 확인</h2><p>롤을 선택하면 위치별 마모와 이벤트 영향을 살펴볼 수 있습니다.</p><div className="dw-roll-buttons">{(['tire_01','tire_02','tire_03'] as ComponentId[]).map((id,i)=><button key={id} onClick={()=>choosePart(id,0)}>롤 {i+1} 상세</button>)}</div></section>:<><ZoneMatrix snapshot={snapshot} selection={selection} onSelect={choosePart}/><OperatingPanel snapshot={snapshot}/><MaintenancePanel record={record} at={at}/></>}
    </>}</aside></div>
    <footer className="dw-page-footer"><span>{data?`${data.title} · 생성 ${date(data.generatedAt)}`:'사양·도면 기반 모델 · 운영 데이터 미연결'}</span><span>역할에 제한 없이 정보 깊이를 전환할 수 있습니다.</span></footer>
  </main>;
}
