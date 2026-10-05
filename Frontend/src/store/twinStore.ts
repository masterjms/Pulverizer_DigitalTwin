import { create } from 'zustand';
import type { ContactWidthScenario } from '../types/api';
import { clearUnitySelection } from '../unity/bridge';

export type TwinMode = 'overview' | 'tire-detail';
export type DataMode = 'live' | 'scenario';
export type BottomTab = 'drawing' | 'heatmap' | 'status';

export interface ScenarioInput {
  contactWidthScenario: ContactWidthScenario;
  wearCoefficientK?: number;
  loadNewtons?: number;
}

interface TwinState {
  mode: TwinMode;
  selectedTireId: string | null;
  dataMode: DataMode;
  scenario: ScenarioInput;
  bottomTab: BottomTab;

  // Unity(또는 지금 단계의 대체 뷰)에서 부품이 선택됐을 때 호출한다.
  selectTire: (tireId: string) => void;
  // 부품 선택 해제 - 빈 공간 클릭 등.
  clearSelection: () => void;
  setDataMode: (mode: DataMode) => void;
  setScenario: (scenario: Partial<ScenarioInput>) => void;
  setBottomTab: (tab: BottomTab) => void;
}

export const useTwinStore = create<TwinState>((set) => ({
  mode: 'overview',
  selectedTireId: null,
  dataMode: 'live',
  scenario: { contactWidthScenario: 'band' },
  bottomTab: 'drawing',

  selectTire: (tireId) =>
    set({ mode: 'tire-detail', selectedTireId: tireId }),

  clearSelection: () => {
    set({ mode: 'overview', selectedTireId: null });
    // Unity에서 온 이벤트로 여기 도달했다면 이미 Unity 쪽은 정리된 상태라 중복 호출이지만,
    // React "닫기" 버튼처럼 HTML에서 먼저 닫는 경우엔 Unity 쪽 하이라이트/줌도 같이 풀어야 한다.
    clearUnitySelection();
  },

  setDataMode: (dataMode) => set({ dataMode }),

  setScenario: (partial) =>
    set((state) => ({ scenario: { ...state.scenario, ...partial } })),

  setBottomTab: (bottomTab) => set({ bottomTab }),
}));
