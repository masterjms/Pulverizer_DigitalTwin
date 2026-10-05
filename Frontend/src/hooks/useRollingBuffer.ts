import { useEffect, useRef, useState } from 'react';

// 지정한 fetcher를 intervalMs마다 호출해서, 최근 windowSize개의 샘플만 들고 있는
// 롤링 버퍼를 만든다. 상태 그래프(최근 60초 등)처럼 클라이언트에서 짧은 이력을
// 직접 쌓아야 할 때 쓴다 - 서버에 아직 저장되지 않는 필드(Journal Pressure 등)도
// 이 방식이면 바로 그래프로 볼 수 있다.
export function useRollingBuffer<T, S>(
  fetcher: () => Promise<T>,
  select: (data: T) => S,
  intervalMs: number,
  windowSize: number,
): S[] {
  const [buffer, setBuffer] = useState<S[]>([]);
  const bufferRef = useRef<S[]>([]);

  useEffect(() => {
    let cancelled = false;

    async function tick() {
      try {
        const data = await fetcher();
        if (cancelled) return;
        const next = [...bufferRef.current, select(data)].slice(-windowSize);
        bufferRef.current = next;
        setBuffer(next);
      } catch {
        // 폴링 실패는 조용히 무시하고 다음 tick에서 재시도한다.
      }
    }

    tick();
    const timer = setInterval(tick, intervalMs);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intervalMs, windowSize]);

  return buffer;
}
