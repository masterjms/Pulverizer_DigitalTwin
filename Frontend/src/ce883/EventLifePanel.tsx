import {useEffect,useMemo,useRef,useState} from 'react';
import type {MachineRecord,MillEvent,Snapshot} from './depthContract';
import {snapshotAt} from './depthContract';
import {sourceLabel} from './contract';
import {fmt,date,timeLabel} from './depthFormat';
import {lifePaths,lifeSeries,lifeValueAt,orderEvents,relevantEvent,severityLabel} from './eventLife';
import type {PartSelection} from './eventLife';
import './eventLife.css';

export function EventLifePanel({record,at,selection,selectedEvent,onEvent,snapshot}:{record:MachineRecord|undefined;at:string;selection:PartSelection;selectedEvent:string|null;onEvent:(e:MillEvent)=>void;snapshot:Snapshot|null}){
  const graphBox=useRef<HTMLDivElement>(null);
  const [size,setSize]=useState({width:920,height:340});
  useEffect(()=>{const box=graphBox.current;if(!box)return;const observer=new ResizeObserver(([entry])=>{const {width,height}=entry.contentRect;if(width>0&&height>0)setSize({width,height});});observer.observe(box);return()=>observer.disconnect();},[]);
  const chartWidth=Math.max(520,size.width),chartHeight=Math.max(130,size.height);
  const plotLeft=48,plotRight=chartWidth-20,plotBottom=chartHeight-65,rail=chartHeight-38;
  const [sort,setSort]=useState<'latest'|'severity'>('latest');
  const [filter,setFilter]=useState('all');
  const [dayFocus,setDayFocus]=useState(false);
  const series=useMemo(()=>lifeSeries(record,selection),[record,selection]);
  const relevant=useMemo(()=>record?.events.filter(e=>relevantEvent(e,selection))??[],[record,selection]);
  const events=orderEvents(relevant.filter(e=>filter==='all'||e.severity===filter),sort);
  const selected=relevant.find(e=>e.id===selectedEvent);
  const anchors=[...series.points.map(p=>Date.parse(p.at)),...relevant.map(e=>Date.parse(e.at)),...(record?.snapshots.map(s=>Date.parse(s.at))??[])];
  const focusDay=Date.parse(new Date(Date.parse(at)+9*3600000).toISOString().slice(0,10)+'T00:00:00+09:00');
  const start=dayFocus?focusDay:anchors.length?Math.min(...anchors):Date.parse(at)-86400000,end=dayFocus?focusDay+86400000:anchors.length?Math.max(...anchors):Date.parse(at);
  const span=Math.max(86400000,end-start),right=start+span;
  const x=(stamp:string)=>plotLeft+(Date.parse(stamp)-start)/span*(plotRight-plotLeft);
  const values=series.points.filter(p=>p.remainingDays!==null);
  const max=values.length?Math.max(1,...values.map(p=>p.remainingDays!))*1.12:1;
  const y=(v:number)=>plotBottom-v/max*(plotBottom-30);
  const paths=lifePaths(series.points,record?.maxGapSeconds??1,x,y);
  const chartEvents=events.filter(e=>Date.parse(e.at)>=start&&Date.parse(e.at)<=right);
  const lifeAt=(stamp:string)=>lifeValueAt(series.points,stamp,record?.maxGapSeconds??1);
  const eventSnapshot=selected?(snapshot?.at===selected.at?snapshot:snapshotAt(record,selected.at)):null;

  const selectedRemaining=selected?lifeAt(selected.at):lifeAt(at);
  const replacement=eventSnapshot?.reliability.replacementAt??(!selected?snapshot?.reliability.replacementAt:null);
  return <section className="el-panel" aria-label="잔여수명과 이벤트 분석">
    <header className="el-heading"><div><h2>잔여수명과 이벤트</h2><p>곡선이 꺾인 시점의 이벤트와 운전 조건을 함께 확인하세요.</p></div><span>{series.label}</span></header>
    <div className="el-workbench"><div className="el-graph-column">
      <div className="el-readout"><div><small>{selected?'이벤트 시점 잔여수명':'선택 시점 잔여수명'}</small><strong>{selectedRemaining===null?'미산정':fmt(selectedRemaining,1)}{selectedRemaining!==null&&<small>일</small>}</strong></div><div><small>교체 권장일</small><b>{replacement?date(replacement):'산정 대기'}</b></div><div><small>관련 이벤트</small><b>{relevant.length}건</b></div></div>
      <div className="el-range"><span>{new Date(start).toLocaleDateString('ko-KR',{timeZone:'Asia/Seoul'})} — {new Date(end).toLocaleDateString('ko-KR',{timeZone:'Asia/Seoul'})}</span><div><button aria-pressed={!dayFocus} onClick={()=>setDayFocus(false)}>전체 기간</button><button aria-pressed={dayFocus} onClick={()=>setDayFocus(true)}>선택 날짜 확대</button></div></div>
      <div ref={graphBox} className="el-graph-scroll"><svg className="el-graph" viewBox={`0 0 ${chartWidth} ${chartHeight}`} role="group" aria-label="날짜별 교체까지 남은 일수와 이벤트. 점을 선택하면 상세 내용과 운전 시점이 바뀝니다.">
        <text x="14" y="23">잔여수명 (일)</text>
        {[0,.25,.5,.75,1].map(f=><g key={f}><line className="el-grid" x1={plotLeft} x2={plotRight} y1={y(max*f)} y2={y(max*f)}/><text x={plotLeft-9} y={y(max*f)+4} textAnchor="end">{values.length?fmt(max*f,0):'—'}</text></g>)}
        <svg x={plotLeft} y="25" width={plotRight-plotLeft} height={plotBottom-20} viewBox={`${plotLeft} 25 ${plotRight-plotLeft} ${plotBottom-20}`}><path d={paths.history} className="el-life-line"/><path d={paths.forecast} className="el-life-line el-forecast"/></svg>
        {values.length===1&&<circle cx={x(values[0].at)} cy={y(values[0].remainingDays!)} r="4" fill="#59cbb7"/>}
        {!values.length&&<g className="el-chart-empty"><text x={chartWidth/2} y={Math.max(48,plotBottom/2)} textAnchor="middle">잔여수명 계산 대기</text><text x={chartWidth/2} y={Math.max(66,plotBottom/2+22)} textAnchor="middle">확인된 이벤트는 아래 시간축에서 먼저 살펴볼 수 있습니다.</text></g>}
        {Date.parse(at)>=start&&Date.parse(at)<=right&&<g><line x1={x(at)} x2={x(at)} y1="25" y2={rail+7} className="el-cursor"/><text x={Math.min(plotRight-40,Math.max(160,x(at)))} y="22" textAnchor="middle">선택 시점</text></g>}
        <line x1={plotLeft} x2={plotRight} y1={rail} y2={rail} className="el-event-rail"/>
        <text x={plotLeft} y={rail-10}>수명값 없는 이벤트</text>
        {chartEvents.map(e=>{const v=lifeAt(e.at);const cy=v===null?rail:y(v);return <g key={e.id} role="button" tabIndex={0} aria-label={`그래프 이벤트 ${timeLabel(e.at)} ${e.title}`} aria-pressed={e.id===selectedEvent} onClick={()=>onEvent(e)} onKeyDown={k=>{if(k.key==='Enter'||k.key===' '){k.preventDefault();onEvent(e);}}} className={`el-marker ${e.kind} ${e.severity??'unrated'} ${e.id===selectedEvent?'selected':''}`}>
          <title>{timeLabel(e.at)} · {e.title} · {e.severity?`심각도 ${severityLabel[e.severity]}`:'심각도 미분류'}{v===null?' · 잔여수명 미산정':` · ${fmt(v)}일 남음`}</title>
          {e.kind==='replacement'&&<line x1={x(e.at)} x2={x(e.at)} y1="30" y2={rail+7} className="el-replacement-line"/>}
          <circle cx={x(e.at)} cy={cy} r="13" className="el-hit"/><circle cx={x(e.at)} cy={cy} r={e.id===selectedEvent?8:5} className="el-dot"/>
        </g>;})}
        {[0,.25,.5,.75,1].map(f=><text key={f} x={plotLeft+(plotRight-plotLeft)*f} y={chartHeight-10} textAnchor={f===0?'start':f===1?'end':'middle'}>{dayFocus?(f===1?'24:00':`${String(f*24).padStart(2,'0')}:00`):new Date(start+span*f).toLocaleDateString('ko-KR',{timeZone:'Asia/Seoul',month:'2-digit',day:'2-digit'})}</text>)}
      </svg></div>
      <div className="el-legend"><span className="history">이력 추정</span><span className="forecast">미래 예측</span><span className="high">심각도 상</span><span className="medium">중</span><span className="low">하</span><span className="replacement">교체</span></div>
      <p className="el-basis">{values.length?'남은 일수는 제공된 교체 예측 기준입니다. 교체 주기·기록 공백을 가로질러 선을 잇지 않습니다.':'운전·정비 이력은 연결할 수 있습니다. 잔여수명 곡선에는 검증된 예측 결과가 필요합니다.'} {selection&&series.label==='설비 교체 기준'?'선택 부품의 개별 수명은 미연결이며, 설비 기준을 표시합니다.':''}</p>
    </div><aside className="el-events" aria-label="그래프의 이벤트 목록"><div className="el-list-heading"><h3>이벤트 내역 <small>{events.length}건</small></h3><label>정렬<select aria-label="이벤트 정렬" value={sort} onChange={e=>setSort(e.target.value as 'latest'|'severity')}><option value="latest">최신순</option><option value="severity">심각도순</option></select></label></div>
      <div className="el-filters" aria-label="심각도 필터">{[['all','전체'],['high','상'],['medium','중'],['low','하']].map(([id,label])=><button key={id} aria-pressed={filter===id} onClick={()=>setFilter(id)}>{label}</button>)}</div>
      <div className="el-event-cards">{events.map(e=><button key={e.id} aria-pressed={e.id===selectedEvent} className={`el-event-card ${e.id===selectedEvent?'selected':''}`} onClick={()=>onEvent(e)}><span><time>{timeLabel(e.at)}</time><em className={e.severity??'unrated'}>{e.severity?`심각도 ${severityLabel[e.severity]}`:'미분류'}</em></span><b>{e.title}</b><small>{sourceLabel[e.source]} · 상세 보기</small></button>)}{!events.length&&<p className="el-list-empty">{relevant.length?'선택한 심각도의 이벤트가 없습니다.':'연결된 이벤트가 없습니다.'}</p>}</div>
    </aside></div>

  </section>;
}

export function EventLifeDetails({record,selectedEvent,snapshot}:{record:MachineRecord|undefined;selectedEvent:string|null;snapshot:Snapshot|null}){
  const selected=record?.events.find(e=>e.id===selectedEvent);
  const eventSnapshot=selected?(snapshot?.at===selected.at?snapshot:snapshotAt(record,selected.at)):null;
  const fields=[['저널 압력 실측',eventSnapshot?.operating.journalActualPsi,'psi'],['급탄량',eventSnapshot?.operating.feedTph,'t/h'],['보울 차압',eventSnapshot?.operating.bowlDpMmH2o,'mmH₂O'],['분급기 속도',eventSnapshot?.operating.classifierRpm,'rpm'],['AMWI',eventSnapshot?.operating.amwi,'무차원'],['3호기 출력',eventSnapshot?.operating.plantMw,'MW']] as const;
  return <section className="dw-section el-side-detail" aria-label="선택 이벤트 상세">
    <div className="el-detail" aria-live="polite">{selected?<><div className="el-detail-heading"><div><small>선택 이벤트 · {timeLabel(selected.at)} KST</small><h3>{selected.title}</h3></div><span>{selected.severity?`심각도 ${severityLabel[selected.severity]}`:'심각도 미분류'}</span></div><p>{selected.note}</p><div className="el-sensors">{fields.map(([name,value,unit])=><div key={name}><small>{name}</small><strong>{fmt(value,2)} <small>{unit}</small></strong></div>)}</div><div className="el-impact"><b>마모율 전후</b><span>{fmt(selected.beforeRateUmPerHour,2)} → {fmt(selected.afterRateUmPerHour,2)} µm/h</span><small>{selected.beforeRateUmPerHour===null||selected.afterRateUmPerHour===null?'전후 마모율 미산정. 이벤트가 줄인 수명은 아직 계산하지 않았습니다.':'관찰된 전후 차이이며, 이벤트의 인과 효과를 확정하지 않습니다.'}</small></div>{!eventSnapshot&&<small>이벤트 시각의 유효 운전 기록이 없습니다. 이전 시점 값을 대신 표시하지 않습니다.</small>}</>:<div className="el-detail-empty"><b>이벤트 점 또는 목록에서 확인할 항목을 선택하세요.</b><p>날짜·부품·재생 시점이 함께 이동하고, 당시 압력·급탄량 등 운전 변수가 여기에 표시됩니다.</p></div>}</div>
    <details className="el-correlation"><summary>마모와 생산량의 상관 분석</summary>{record?.correlation?<p>상관계수 {fmt(record.correlation.coefficient,3)} · 표본 {record.correlation.sampleCount}개 · {record.correlation.method} · {record.correlation.period}<br/>{record.correlation.note} · {sourceLabel[record.correlation.source]}<br/>상관관계만으로 이벤트의 인과 효과나 통계적 유의성을 확정하지 않습니다.</p>:<p>분석 결과 미연결. 같은 기간의 마모·생산량과 분석 근거가 필요합니다.</p>}</details>
  </section>;
}
