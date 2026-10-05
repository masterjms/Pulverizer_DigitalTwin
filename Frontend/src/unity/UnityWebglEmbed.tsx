import { useEffect, useRef, useState } from 'react';
import { setUnityInstance } from './bridge';

// 실제 Unity WebGL 빌드 임베드. PulverizerTwin 프로젝트를
// Tools/Pulverizer Twin/Build WebGL 로 빌드한 뒤, 산출물(Build/, TemplateData/)을
// public/unity-build/ 아래로 복사해서 Vite가 정적 파일로 서빙하게 한다.
//
// 빌드 파일명은 PlayerSettings.productName에서 파생되므로, 실제 빌드 후 아래
// BUILD_NAME이 산출물 파일명과 정확히 일치하는지 확인이 필요하다.
const BUILD_NAME = 'WebGL';
const BUILD_BASE = `${import.meta.env.BASE_URL}unity-build/Build`;

declare global {
  interface Window {
    createUnityInstance?: (
      canvas: HTMLCanvasElement,
      config: Record<string, unknown>,
      onProgress?: (progress: number) => void,
    ) => Promise<{ SendMessage: (objectName: string, methodName: string, value?: string | number) => void }>;
  }
}

export function UnityWebglEmbed() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const scriptSrc = `${BUILD_BASE}/${BUILD_NAME}.loader.js`;
    const script = document.createElement('script');
    script.src = scriptSrc;

    script.onload = async () => {
      if (cancelled || !canvasRef.current || !window.createUnityInstance) return;

      try {
        const instance = await window.createUnityInstance(
          canvasRef.current,
          {
            dataUrl: `${BUILD_BASE}/${BUILD_NAME}.data`,
            frameworkUrl: `${BUILD_BASE}/${BUILD_NAME}.framework.js`,
            codeUrl: `${BUILD_BASE}/${BUILD_NAME}.wasm`,
            streamingAssetsUrl: 'StreamingAssets',
            companyName: 'NEXTRO',
            productName: BUILD_NAME,
          },
          (p) => !cancelled && setProgress(p),
        );

        if (!cancelled) {
          setUnityInstance(instance);
          setLoaded(true);
          // Chrome: 캔버스가 포커스를 먼저 가져야 첫 클릭부터 Raycast(부품 선택)가
          // 정상 동작한다 - 레퍼런스(ovzexx/CoalPulverizer3D)의 index.html에서 확인된 처리.
          canvasRef.current?.focus();
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      }
    };

    script.onerror = () => {
      if (!cancelled) setError(`Unity 로더 스크립트를 불러오지 못했습니다: ${scriptSrc}`);
    };

    document.body.appendChild(script);

    return () => {
      cancelled = true;
      document.body.removeChild(script);
      setUnityInstance(null);
    };
  }, []);

  return (
    <div className="relative h-full w-full bg-slate-950">
      <canvas id="unity-canvas" ref={canvasRef} className="h-full w-full" tabIndex={-1} />

      {!loaded && !error && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-slate-950">
          <div className="text-sm text-slate-400">미분기 3D 모델 불러오는 중... {Math.round(progress * 100)}%</div>
          <div className="h-1.5 w-64 overflow-hidden rounded bg-slate-800">
            <div className="h-full bg-sky-500 transition-all" style={{ width: `${progress * 100}%` }} />
          </div>
        </div>
      )}

      {error && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-slate-950 p-4 text-center">
          <div className="text-sm font-medium text-rose-400">Unity WebGL 로드 실패</div>
          <div className="max-w-md text-xs text-slate-500">{error}</div>
        </div>
      )}
    </div>
  );
}
