import { useEffect, useRef, useState } from 'react';
import type { RefObject } from 'react';
import type { MachineId,ComponentId,WearFrame } from './contract';
import type { Depth,RunStatus } from './depthContract';
type Unity={SendMessage:(object:string,method:string,value?:string)=>void;Quit:()=>Promise<void>};
export type UnityNotice={type:string;machineId?:MachineId;partId?:ComponentId;zone?:number;message?:string};
export interface ViewState {machine:MachineId;depth:Depth;selection:{partId:ComponentId;zone:number}|null;at:string;frame:WearFrame|null;fleet:{machineId:MachineId;status:RunStatus}[];}
export function useDepthUnity(canvas:RefObject<HTMLCanvasElement>,state:ViewState,onNotice:(n:UnityNotice)=>void){
  const [loading,setLoading]=useState('모델 불러오는 중');const [error,setError]=useState('');
  const instance=useRef<Unity|null>(null);const ready=useRef(false);const latest=useRef(state);latest.current=state;
  const listener=useRef(onNotice);listener.current=onNotice;
  const previous=useRef<ViewState|null>(null);
  const send=(method:string,value?:string)=>{if(ready.current)instance.current?.SendMessage('NextroTwinBridge',method,value);};
  const sync=useRef(()=>{});
  sync.current=()=>{
    if(!ready.current)return;
    const s=latest.current,p=previous.current;
    const machineChanged=!p||p.machine!==s.machine;
    if(machineChanged)send('FocusMachine',JSON.stringify({machineId:s.machine}));
    if(machineChanged||p?.depth!==s.depth||JSON.stringify(p?.selection)!==JSON.stringify(s.selection)){
      send('SetView',s.depth);
      if(s.depth==='exploded'&&s.selection)send('FocusComponent',JSON.stringify({machineId:s.machine,...s.selection}));
    }
    if(machineChanged||p?.depth!==s.depth||p?.frame!==s.frame){if(s.frame)send('ApplyWearState',JSON.stringify(s.frame));else send('ClearData');}
    send('ApplyFleetState',JSON.stringify({machines:s.fleet}));send('SetTimeCursor',s.at);previous.current=s;
  };
  useEffect(()=>{sync.current();},[state]);
  useEffect(()=>{
    let disposed=false,local:Unity|null=null,gotReady=false,readyTimeout:number|undefined;
    const motion=window.matchMedia('(prefers-reduced-motion: reduce)');
    const updateMotion=()=>{if(ready.current&&local)local.SendMessage('NextroTwinBridge','SetMotionEnabled',motion.matches?'0':'1');};
    motion.addEventListener('change',updateMotion);
    const flush=()=>{if(disposed||!local||!gotReady)return;window.clearTimeout(readyTimeout);instance.current=local;ready.current=true;local.SendMessage('NextroTwinBridge','EnableBrowserPicking','1');updateMotion();sync.current();setLoading('');};
    const handler=(e:Event)=>{const notice=(e as CustomEvent<UnityNotice>).detail;if(!notice)return;if(notice.type==='ready'){gotReady=true;flush();}else listener.current(notice);};
    window.addEventListener('nextro-twin',handler);
    const base=`${import.meta.env.BASE_URL}ce883-build`;const script=document.createElement('script');let revision='';
    const asset=(name:string)=>`${base}/Build/${name}?v=${revision}`;
    script.onload=async()=>{
      if(disposed||!canvas.current)return;
      try{const create=(window as unknown as {createUnityInstance:(c:HTMLCanvasElement,config:object,progress:(n:number)=>void)=>Promise<Unity>}).createUnityInstance;
        local=await create(canvas.current,{dataUrl:asset('CE883.data'),frameworkUrl:asset('CE883.framework.js'),codeUrl:asset('CE883.wasm'),streamingAssetsUrl:`${base}/StreamingAssets`,companyName:'NEXTRO',productName:'CE883 Depth Workspace',productVersion:revision},n=>{if(!disposed)setLoading(`모델 불러오는 중 ${Math.round(n*100)}%`);});
        if(disposed){await local.Quit();return;}if(!gotReady)readyTimeout=window.setTimeout(()=>{if(!disposed&&!gotReady){setError('Unity 씬 초기화가 완료되지 않았습니다. 새로고침 후에도 반복되면 빌드 실행 로그를 확인해 주세요.');setLoading('');}},15000);flush();
      }catch(e){if(!disposed){setError(e instanceof Error?e.message:'Unity 실행 실패');setLoading('');}}
    };
    script.onerror=()=>{if(!disposed){setError('Unity 빌드 파일을 읽지 못했습니다. 페이지를 다시 열어 주세요.');setLoading('');}};
    void (async()=>{try{
      const response=await fetch(`${base}/build-version.json`,{cache:'no-store',signal:AbortSignal.timeout(15000)});
      if(!response.ok)throw new Error('Unity 빌드 버전 파일이 없습니다. Publish-Ce883Local.ps1을 실행하세요.');
      const version=await response.json() as {revision?:string};
      if(typeof version.revision!=='string'||!/^[A-Fa-f0-9-]+$/.test(version.revision))throw new Error('Unity 빌드 버전 형식 오류');
      if(disposed)return;revision=version.revision;script.src=asset('CE883.loader.js');document.body.appendChild(script);
    }catch(e){if(!disposed){setError(e instanceof Error?e.message:'Unity 버전 조회 실패');setLoading('');}}})();
    return()=>{disposed=true;motion.removeEventListener('change',updateMotion);window.clearTimeout(readyTimeout);ready.current=false;instance.current=null;previous.current=null;window.removeEventListener('nextro-twin',handler);script.remove();if(local)void local.Quit();};
  },[canvas]);
  return {loading,error,send};
}
