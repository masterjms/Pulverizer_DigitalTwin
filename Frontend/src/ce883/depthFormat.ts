export const fmt=(n:number|null|undefined,digits=1)=>n==null?'—':n.toLocaleString('ko-KR',{maximumFractionDigits:digits});
export const date=(at:string|null|undefined)=>at?new Date(at).toLocaleDateString('ko-KR',{timeZone:'Asia/Seoul',year:'numeric',month:'2-digit',day:'2-digit'}):'산정 자료 없음';
export const timeLabel=(at:string)=>new Date(at).toLocaleString('ko-KR',{timeZone:'Asia/Seoul',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hour12:false});
