import type {
  ContactWidthScenario,
  HistoryResponse,
  PulverizerStatus,
  PulverizerWearResponse,
} from '../types/api';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:8000';

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`);

  if (!response.ok) {
    throw new Error(`API 요청 실패 (${response.status}): ${path}`);
  }

  return response.json() as Promise<T>;
}

export function fetchStatus(machineId: string): Promise<PulverizerStatus> {
  return getJson(`/api/pulverizers/${machineId}/status`);
}

export function fetchWear(
  machineId: string,
  options?: {
    contactWidthScenario?: ContactWidthScenario;
    wearCoefficientK?: number;
    loadNewtons?: number;
  },
): Promise<PulverizerWearResponse> {
  const params = new URLSearchParams();

  if (options?.contactWidthScenario) {
    params.set('contact_width_scenario', options.contactWidthScenario);
  }
  if (options?.wearCoefficientK !== undefined) {
    params.set('wear_coefficient_k', String(options.wearCoefficientK));
  }
  if (options?.loadNewtons !== undefined) {
    params.set('load_newtons', String(options.loadNewtons));
  }

  const query = params.toString();

  return getJson(`/api/pulverizers/${machineId}/wear${query ? `?${query}` : ''}`);
}

export function fetchHistory(machineId: string, minutes = 30): Promise<HistoryResponse> {
  return getJson(`/api/pulverizers/${machineId}/history?minutes=${minutes}`);
}
