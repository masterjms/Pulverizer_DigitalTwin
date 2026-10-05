export type MachineId = `pulverizer_${'a' | 'b' | 'c' | 'd' | 'e' | 'f'}`;
export type ComponentId = 'tire_01' | 'tire_02' | 'tire_03';
export interface WearFrame {
  schemaVersion: 1;
  machineId: MachineId;
  asOf: string;
  source: 'measured' | 'model' | 'forecast' | 'scenario' | 'experiment';
  displayMode: 'live' | 'history' | 'forecast' | 'scenario';
  running: boolean;
  bowlRpm: number;
  classifierRpm: number;
  wearLimitMm: number;
  rollers: { componentId: ComponentId; wearMm: number[]; valid: boolean[] }[];
}

// 구조 검증만 한다. 마모 추정과 수명 계산은 백엔드의 책임이다.
export function validateFrame(input: unknown, machineId: MachineId): WearFrame {
  if (!input || typeof input !== 'object') throw new Error('스냅샷 객체가 필요합니다.');
  const f = input as WearFrame;
  if (f.schemaVersion !== 1) throw new Error('지원하지 않는 스키마 버전입니다.');
  if (f.machineId !== machineId) throw new Error('선택 설비와 스냅샷의 설비가 다릅니다.');
  if (typeof f.asOf !== 'string' || !/(Z|[+-]\d{2}:\d{2})$/.test(f.asOf) || !Number.isFinite(Date.parse(f.asOf))) throw new Error('시간대가 포함된 기준 시각이 필요합니다.');
  if (!['measured', 'model', 'forecast', 'scenario', 'experiment'].includes(f.source)) throw new Error('데이터 출처가 필요합니다.');
  if (!['live', 'history', 'forecast', 'scenario'].includes(f.displayMode)) throw new Error('표시 모드가 올바르지 않습니다.');
  if (typeof f.running !== 'boolean') throw new Error('가동 상태가 필요합니다.');
  if (![f.bowlRpm, f.classifierRpm, f.wearLimitMm].every(v => typeof v === 'number' && Number.isFinite(v) && v >= 0) || f.wearLimitMm === 0) throw new Error('회전수 또는 마모 한계가 올바르지 않습니다.');
  if (!Array.isArray(f.rollers) || f.rollers.length !== 3) throw new Error('롤 3개를 모두 전달해야 합니다.');
  for (const id of ['tire_01', 'tire_02', 'tire_03']) {
    const matches = f.rollers.filter(r => r?.componentId === id);
    if (matches.length !== 1) throw new Error('롤 식별자가 중복되었거나 누락되었습니다.');
    const r = matches[0];
    if (!Array.isArray(r.wearMm) || !Array.isArray(r.valid) || r.wearMm.length !== 10 || r.valid.length !== 10) throw new Error('롤마다 10개 위치와 유효성 표시가 필요합니다.');
    if (!r.wearMm.every(v => typeof v === 'number' && Number.isFinite(v) && v >= 0) || !r.valid.every(v => typeof v === 'boolean')) throw new Error('마모값 또는 유효성 표시가 올바르지 않습니다.');
  }
  return structuredClone(f);
}

export const sourceLabel: Record<WearFrame['source'], string> = { measured: '실측', model: '모델', forecast: '예측', scenario: '시나리오', experiment: '실험(PINN)' };
export function wearColor(mm: number, limit: number, valid: boolean): string {
  if (!valid) return '#4f5c63';
  const ratio = mm / limit;
  return ratio < .35 ? '#33a392' : ratio < .60 ? '#dbb844' : ratio < .80 ? '#ed6e29' : '#d12b29';
}
