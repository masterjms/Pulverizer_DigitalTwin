import { useEffect } from 'react';
import { subscribeUnityBridge } from './bridge';
import { useTwinStore } from '../store/twinStore';

// Unity(또는 placeholder) 브리지 이벤트를 twinStore 상태 전환으로 연결한다.
// App 최상단에서 한 번만 마운트하면 된다.
export function useUnityBridgeSync(): void {
  const selectTire = useTwinStore((state) => state.selectTire);
  const clearSelection = useTwinStore((state) => state.clearSelection);

  useEffect(() => {
    return subscribeUnityBridge((event) => {
      if (event.type === 'partSelected') {
        selectTire(event.partId);
      } else if (event.type === 'selectionCleared') {
        clearSelection();
      }
    });
  }, [selectTire, clearSelection]);
}
