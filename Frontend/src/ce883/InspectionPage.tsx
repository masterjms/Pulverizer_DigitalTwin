import { useEffect, useRef, useState } from 'react';
import { sourceLabel, validateFrame, wearColor } from './contract';
import type { ComponentId, MachineId, WearFrame } from './contract';
import './inspection.css';

type Unity = { SendMessage: (object: string, method: string, value?: string) => void; Quit: () => Promise<void> };
type Notice = { type: string; machineId?: string; partId?: ComponentId; zone?: number; message?: string };
const base = `${import.meta.env.BASE_URL}ce883-build`;
const modeLabel = {live:'현재',history:'과거',forecast:'미래 예측',scenario:'가상 조건'};

export function InspectionPage() {
  const canvas = useRef<HTMLCanvasElement>(null);
  const pointer = useRef<{id:number;x:number;y:number;moved:boolean}|null>(null);
  const instance = useRef<Unity | null>(null);
  const ready = useRef(false);
  const alive = useRef(false);
  const [loadState, setLoadState] = useState('Unity 모델을 불러오는 중');
  const [loadFailed, setLoadFailed] = useState(false);
  const [machine, setMachine] = useState<MachineId>('pulverizer_b');
  const currentMachine = useRef(machine); currentMachine.current = machine;
  const [view, setView] = useState('exterior');
  const [selected, setSelected] = useState<ComponentId | null>(null);
  const [zone, setZone] = useState<number | null>(null);
  const [frame, setFrame] = useState<WearFrame | null>(null);
  const [error, setError] = useState('');
  const [motion, setMotion] = useState(false);
  const [endpoint, setEndpoint] = useState('');
  const [busy, setBusy] = useState(false);
  const generation = useRef(0);
  const desired = useRef({machine, view, frame, motion, selected, zone}); desired.current = {machine, view, frame, motion, selected, zone};

  function send(method: string, value?: string) { if (ready.current) instance.current?.SendMessage('NextroTwinBridge', method, value); }
  useEffect(() => {
    alive.current = true;
    let disposed = false;
    let local: Unity | null = null;
    let gotReady = false;
    const flush = () => {
      if (!gotReady || !local || disposed) return;
      ready.current = true; instance.current = local;
      local.SendMessage('NextroTwinBridge','EnableBrowserPicking','1');
      const state = desired.current;
      local.SendMessage('NextroTwinBridge','FocusMachine',JSON.stringify({machineId:state.machine}));
      local.SendMessage('NextroTwinBridge','SetView',state.view);
      if (state.frame) local.SendMessage('NextroTwinBridge','ApplyWearState',JSON.stringify(state.frame));
      if (state.view === 'component' && state.selected) local.SendMessage('NextroTwinBridge','FocusComponent',JSON.stringify({machineId:state.machine,partId:state.selected,zone:state.zone ?? 0}));
      local.SendMessage('NextroTwinBridge','SetPreviewMotion',state.motion ? '1' : '0');
      setLoadState('');
    };
    const listener = (event: Event) => {
      const detail = (event as CustomEvent<Notice>).detail;
      if (!detail) return;
      if (detail.type === 'ready') { gotReady = true; flush(); }
      else if (detail.type === 'partSelected' && detail.machineId === currentMachine.current) { setSelected(detail.partId ?? null); setZone(detail.zone ?? null); }
      else if (detail.type === 'selectionCleared') {setSelected(null);setZone(null);setView('exterior');}
      else if (detail.type === 'error') setError(detail.message ?? 'Unity 수신 오류');
    };
    window.addEventListener('nextro-twin', listener);
    const script = document.createElement('script'); script.src = `${base}/Build/CE883.loader.js`;
    script.onload = async () => {
      if (disposed || !canvas.current) return;
      try {
        const create = (window as unknown as {createUnityInstance: (c: HTMLCanvasElement, config: object, progress: (n:number)=>void)=>Promise<Unity>}).createUnityInstance;
        local = await create(canvas.current, {dataUrl:`${base}/Build/CE883.data`,frameworkUrl:`${base}/Build/CE883.framework.js`,codeUrl:`${base}/Build/CE883.wasm`,streamingAssetsUrl:`${base}/StreamingAssets`,companyName:'NEXTRO',productName:'CE883 Inspection',productVersion:'1.0.0'}, n => {if (!disposed) setLoadState(`Unity 모델을 불러오는 중 ${Math.round(n*100)}%`);});
        if (disposed) { await local.Quit(); return; }
        flush();
      } catch (e) { if (!disposed) {setLoadFailed(true);setLoadState(e instanceof Error ? e.message : 'Unity 실행 실패');} }
    };
    script.onerror = () => {if(!disposed){setLoadFailed(true);setLoadState('Unity WebGL 빌드 파일을 불러오지 못했습니다.');}};
    document.body.appendChild(script);
    return () => { disposed = true; alive.current = false; ready.current = false; instance.current = null; window.removeEventListener('nextro-twin',listener);script.remove();if(local)void local.Quit(); };
  }, []);

  function switchMachine(id: MachineId) {
    generation.current++; setMachine(id); currentMachine.current=id;setFrame(null);setSelected(null);setZone(null);setView('exterior');setMotion(false);setError('');
    send('FocusMachine',JSON.stringify({machineId:id}));
  }
  function setMode(mode: string) {setView(mode);send('SetView',mode);}
  function focus(id: ComponentId, position = 0) {setSelected(id);setZone(position || null);setView('component');send('FocusComponent',JSON.stringify({machineId:machine,partId:id,zone:position}));}
  async function importFile(file?: File) {
    if(!file)return;
    const token=++generation.current; const requested=machine;
    try {
      if(file.size>1_000_000)throw new Error('스냅샷 파일은 1 MB 이하로 전달해 주세요.');
      const next=validateFrame(JSON.parse(await file.text()),requested);
      if(token!==generation.current || !alive.current)return;
      setFrame(next);setMotion(false);setError('');send('ApplyWearState',JSON.stringify(next));
    }catch(e){if(token===generation.current)setError(e instanceof Error?e.message:'파일 읽기 실패');}
  }
  async function fetchSnapshot() {
    const token=++generation.current;const requested=machine;setBusy(true);setError('');
    try {
      const url=new URL(endpoint,location.origin);
      if(url.origin!==location.origin)throw new Error('동일 출처의 읽기 전용 API 경로를 입력해 주세요.');
      const response=await fetch(url,{signal:AbortSignal.timeout(15000)});
      if(!response.ok)throw new Error(`스냅샷 조회 실패 (${response.status})`);
      const next=validateFrame(await response.json(),requested);
      if(token!==generation.current || !alive.current)return;
      setFrame(next);setMotion(false);send('ApplyWearState',JSON.stringify(next));
    }catch(e){if(token===generation.current)setError(e instanceof Error?e.message:'조회 실패');}
    finally{if(alive.current)setBusy(false);}
  }

  return <main className="ce-inspection">
    <header className="ce-header"><a href={import.meta.env.BASE_URL}>nextro<span>설비 디지털 트윈</span></a><div className="ce-title"><h1>CE 883HP 미분기</h1><span>보령 3호기 · 모델 검토</span></div><span className="ce-model-badge">도면 기반 외형 모델</span></header>
    <div className="ce-workspace">
      <section className="ce-viewport" aria-label="미분기 3D 모델">
        <div className="ce-toolbar"><div>{(['exterior','cutaway','exploded'] as const).map((v,i)=><button key={v} className={view===v?'active':''} onClick={()=>setMode(v)}>{['외형','절개','분해'][i]}</button>)}</div><div><button onClick={()=>send('SetCamera','iso')}>기본 시점</button><button onClick={()=>send('SetCamera','front')}>정면</button><button onClick={()=>send('SetCamera','top')}>평면</button></div></div>
        <canvas ref={canvas} id="ce883-unity-canvas" tabIndex={0} aria-label="드래그로 회전, 휠로 확대, 롤타이어 클릭으로 위치 선택"
          onPointerDownCapture={e=>{pointer.current=e.button===0?{id:e.pointerId,x:e.clientX,y:e.clientY,moved:false}:null;}}
          onPointerMoveCapture={e=>{const p=pointer.current;if(p&&p.id===e.pointerId&&Math.hypot(e.clientX-p.x,e.clientY-p.y)>5)p.moved=true;}}
          onPointerCancelCapture={()=>{pointer.current=null;}}
          onPointerUpCapture={e=>{const p=pointer.current;pointer.current=null;if(!p||p.id!==e.pointerId||p.moved||Math.hypot(e.clientX-p.x,e.clientY-p.y)>5)return;const r=e.currentTarget.getBoundingClientRect();send('PickViewport',JSON.stringify({x:(e.clientX-r.left)/r.width,y:1-(e.clientY-r.top)/r.height}));}}
        />
        {loadState && <div className="ce-load"><strong>{loadFailed?'Unity 모델을 열 수 없습니다':loadState}</strong>{loadFailed&&<><p>{loadState}</p><p className="ce-small">빌드 파일과 연결 상태를 확인한 뒤 페이지를 새로고침해 주세요. 데이터 입력과 위치별 값 확인은 우측 패널에서 가능합니다.</p></>}</div>}
        <div className="ce-view-label"><span>미분기 {machine.slice(-1).toUpperCase()}{view==='component'&&selected?` / 롤 ${Number(selected.slice(-2))} 확대`:''}</span><strong>{motion?'회전 연출 미리보기':frame?<>{sourceLabel[frame.source]} · {modeLabel[frame.displayMode]}</>:'운전·마모 데이터 미연결'}</strong></div>
        <footer className="ce-canvas-footer"><span>드래그 회전 · 휠 확대 · 롤타이어 선택</span><button onClick={()=>{setMotion(!motion);send('SetPreviewMotion',motion?'0':'1');}}>{motion?'연출 정지':'회전 연출 보기'}</button></footer>
      </section>
      <aside className="ce-panel">
        <section><h2>설비 선택</h2><div className="ce-machines">{['a','b','c','d','e','f'].map(c=><button key={c} className={machine===`pulverizer_${c}`?'active':''} onClick={()=>switchMachine(`pulverizer_${c}` as MachineId)}>{c.toUpperCase()}</button>)}</div><p className="ce-small">동일 형식의 설비 모델을 공유합니다. 데이터는 선택한 설비의 스냅샷만 받습니다.</p></section>
        <section><h2>롤타이어 세트 <span>3개 · 30개 위치</span></h2><div className="ce-rollers">{(['tire_01','tire_02','tire_03'] as const).map((id,i)=><button className={selected===id?'active':''} key={id} onClick={()=>focus(id)}>롤 {i+1}</button>)}</div>
          <div className="ce-zones">{(['tire_01','tire_02','tire_03'] as const).map((id,i)=>{const r=frame?.rollers.find(x=>x.componentId===id);return <div key={id}><label>R{i+1}</label>{Array.from({length:10},(_,j)=><button key={j} title={r?.valid[j]?`위치 ${j+1}: ${r.wearMm[j]} mm`:`위치 ${j+1}: 데이터 없음`} style={{background:r&&frame?wearColor(r.wearMm[j],frame.wearLimitMm,r.valid[j]):'#4f5c63'}} className={selected===id&&zone===j+1?'selected':''} onClick={()=>focus(id,j+1)}>{j+1}</button>)}</div>;})}</div>
          <p className="ce-small">위치 1: 내측 좁은 끝 · 위치 10: 외측 넓은 끝<br/>계측 위치와의 최종 대응은 현장 확인이 필요합니다.</p>
          {selected&&zone&&<p className="ce-selected">롤 {Number(selected.slice(-2))} / 위치 {zone} <b>{frame?.rollers.find(r=>r.componentId===selected)?.valid[zone-1]?`${frame.rollers.find(r=>r.componentId===selected)!.wearMm[zone-1]} mm`:'데이터 없음'}</b></p>}
          <div className="ce-legend"><span>낮음</span>{['#33a392','#dbb844','#ed6e29','#d12b29'].map(c=><i key={c} style={{background:c}}/>)}<span>한계 접근</span></div>
          <p className="ce-small">색 경계: 한계의 35 / 60 / 80% · 회색: 데이터 없음</p>
        </section>
        <section><h2>기준 시점과 출처</h2>{frame?<><p>{new Date(frame.asOf).toLocaleString('ko-KR',{timeZone:'Asia/Seoul'})} KST</p><p>{sourceLabel[frame.source]} · {frame.running?'가동':'정지'} · 마모 한계 {frame.wearLimitMm} mm</p>{Date.now()-Date.parse(frame.asOf)>7*86400000&&<p className="ce-warning">7일 이상 지난 데이터 · 갱신 필요</p>}</>:<p className="ce-muted">스냅샷을 연결하면 롤별 상태가 표시됩니다.<br/>잔여수명은 이 화면에서 계산하지 않습니다.</p>}
          <label className="ce-file">스냅샷 JSON 불러오기<input type="file" accept=".json,application/json" onChange={e=>{void importFile(e.target.files?.[0]);e.target.value='';}}/></label>
          <details><summary>백엔드 스냅샷 연결</summary><label className="ce-small">동일 출처 API 경로<input value={endpoint} onChange={e=>setEndpoint(e.target.value)} placeholder="/api/v1/..." /></label><button disabled={!endpoint||busy} onClick={()=>void fetchSnapshot()}>{busy?'불러오는 중':'조회하여 적용'}</button></details>
          {error&&<p role="alert" className="ce-error">{error}</p>}
        </section>
        <section className="ce-spec"><h2>원본 사양</h2><dl><dt>형식</dt><dd>KHIC / CE · 883HP</dd><dt>롤 외경 / 폭</dt><dd>1,422 / 483 mm</dd><dt>본체 내경</dt><dd>3,150 mm</dd><dt>보울 회전수</dt><dd>41.6 rpm (정격)</dd><dt>분탄 출구</dt><dd>4개 · 내경 533 mm</dd></dl><p className="ce-small">출처: 붙임6. 미분기 스펙, 붙임7. 미분기 도면.<br/>세부 부속 형상·색상은 시각화용 근사입니다.</p></section>
      </aside>
    </div>
  </main>;
}
