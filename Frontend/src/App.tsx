import { Header } from './components/layout/Header';
import { OverviewPanel } from './components/overview/OverviewPanel';
import { TrendStrip } from './components/overview/TrendStrip';
import { TireDetailPanel } from './components/tireDetail/TireDetailPanel';
import { TireDetailBottomStrip } from './components/tireDetail/TireDetailBottomStrip';
import { UnityWebglEmbed } from './unity/UnityWebglEmbed';
import { PartSelectorOverlay } from './unity/PartSelectorOverlay';
import { useUnityBridgeSync } from './unity/useUnityBridgeSync';
import { useTwinStore } from './store/twinStore';
import { DepthWorkspace } from './ce883/DepthWorkspace';

function App() {
  if (new URLSearchParams(window.location.search).get('viewer') === 'ce883') return <DepthWorkspace />;
  return <LegacyApp />;
}

function LegacyApp() {
  useUnityBridgeSync();
  const mode = useTwinStore((state) => state.mode);

  return (
    <div className="flex h-screen w-screen flex-col overflow-hidden bg-[#0d1117] text-[#e6edf3]">
      <Header />
      <div className="flex min-h-0 flex-1">
        <div className="relative min-w-0 flex-1 bg-[#050a10]">
          <UnityWebglEmbed />
          <PartSelectorOverlay />
        </div>
        <div className="w-[380px] shrink-0 border-l border-[#30363d] bg-[#161b22]">
          {mode === 'overview' ? <OverviewPanel /> : <TireDetailPanel />}
        </div>
      </div>
      {mode === 'overview' ? <TrendStrip /> : <TireDetailBottomStrip />}
    </div>
  );
}

export default App;
