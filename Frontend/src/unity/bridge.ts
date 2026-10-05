// Unity WebGL <-> React 브리지 계약.
//
// 실제 Unity 모델(내일 최종본 예정)이 준비되면, Unity C# 쪽에서 .jslib 플러그인으로
// 아래 window 콜백을 호출하도록 연결한다. 지금은 PlaceholderTwinViewer가 같은 이벤트를
// 발생시켜 파이프라인 전체(클릭 -> 상태전환 -> UI 레이아웃 변경)를 미리 검증한다.
//
// Unity -> React: window.__pulverizerTwinBridge.onPartSelected(partId) 등을 호출
// React -> Unity: sendToUnity(objectName, methodName, value) 로 unityInstance.SendMessage 래핑
// (실제 unityInstance가 생기면 setUnityInstance()로 등록)

export type UnityBridgeEvent =
  | { type: 'partSelected'; partId: string }
  | { type: 'selectionCleared' };

type BridgeListener = (event: UnityBridgeEvent) => void;

const listeners = new Set<BridgeListener>();

// Unity(또는 placeholder)가 호출하는 진입점.
export function emitUnityBridgeEvent(event: UnityBridgeEvent): void {
  listeners.forEach((listener) => listener(event));
}

export function subscribeUnityBridge(listener: BridgeListener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

// ---- React -> Unity 방향 (MVP 범위 밖이지만 구조상 미리 준비해둔다) ----
interface UnityInstanceLike {
  SendMessage: (objectName: string, methodName: string, value?: string | number) => void;
}

let unityInstance: UnityInstanceLike | null = null;

export function setUnityInstance(instance: UnityInstanceLike | null): void {
  unityInstance = instance;
}

export function sendToUnity(objectName: string, methodName: string, value?: string | number): void {
  if (!unityInstance) {
    console.warn(
      `[unity-bridge] Unity 인스턴스가 아직 없습니다 (${objectName}.${methodName} 호출 무시됨). ` +
        'PlaceholderTwinViewer 사용 중이거나 실제 Unity WebGL 빌드가 로드되지 않았습니다.',
    );
    return;
  }

  unityInstance.SendMessage(objectName, methodName, value);
}

// React 쪽 "닫기" 버튼 등 HTML UI에서 선택 해제할 때, Unity 쪽 하이라이트/줌도
// 같이 풀어준다 (안 그러면 3D는 확대된 채로 멈춰있고 패널만 닫히는 불일치가 생김).
export function clearUnitySelection(): void {
  sendToUnity('PartInteraction_Manager', 'ClearSelectionFromJS');
}

// 브라우저 전역에 노출 - 실제 Unity .jslib 플러그인이 이 경로로 콜백한다.
declare global {
  interface Window {
    __pulverizerTwinBridge?: {
      onPartSelected: (partId: string) => void;
      onSelectionCleared: () => void;
    };
  }
}

window.__pulverizerTwinBridge = {
  onPartSelected: (partId: string) => emitUnityBridgeEvent({ type: 'partSelected', partId }),
  onSelectionCleared: () => emitUnityBridgeEvent({ type: 'selectionCleared' }),
};
