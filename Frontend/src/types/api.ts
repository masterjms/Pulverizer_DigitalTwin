// FastAPI 응답 스키마 - Backend/main.py, wear_engine.py 와 1:1로 맞춘 타입.
// 필드가 추가/변경되면 이 파일도 같이 갱신한다.
// TODO: 안정화되면 openapi-typescript로 자동 생성 전환 고려.

export interface PulverizerStatus {
  timestamp: string;
  machineId: string;
  isRunning: boolean;

  // 레거시 필드 (기존 Unity PulverizerData 호환)
  bowlRpm: number;
  motorCurrent: number;
  bearingTemp: number;
  vibration: number;
  tireWear: number;
  alarmLevel: 'NORMAL' | 'WARNING' | 'ALARM';
  alarmMessage: string;

  // 입력데이터셋 설명_미분기_20260604.xlsx 기준 14개 실시간 태그
  mw: number;
  feedRateTph: number;
  inletTempC: number;
  outletTempC: number;
  bowlDpMmH2o: number;
  classifierSpeedDemandPct: number;
  classifierVaneSpeedActualRpm: number;
  classifierSpeedBiasPct: number;
  pyriteValveOpen: boolean;
  pyriteHopperTempC: number;
  journalPressureSetpointPsi: number;
  journalPressureActualPsi: number;
  cumulativeOperatingHours: number;
}

export type ContactWidthScenario = 'band' | 'full_width' | 'partial_sweep_30pct';

export interface TireWearResult {
  tireId: string;
  cumulativeHours: number;
  contactWidthScenario: ContactWidthScenario;
  wearCoefficientK: number;
  loadNewtons: number;
  loadEstimationMethod: string;
  amplificationC: number;
  wearMm: number;
  wearLimitMm: number;
  wearRateMmPerHour: number;
  remainingUsefulLifeHours: number | null;
  damagePercent: number;
  severityMultiplier: number;
  notes: string[];
  // Zone 1(내측 좁은 끝) ~ Zone 10(외측 넓은 끝) 마모 분포. 존별 센서가 없어
  // 전체 평균 마모량 + 접촉폭 시나리오 형태로 만든 예시 분포 - 실측 아님.
  zoneWearProfileMm: number[];
}

export interface PulverizerWearResponse {
  machineId: string;
  equation: string;
  equationConstants: {
    bowlTrackDiameterM: number;
    bowlRpm: number;
    overlayHardnessPa: number;
    contactLengthM: number;
    wearLimitMm: number;
  };
  contactWidthScenarios: Record<ContactWidthScenario, number>;
  // 롤타이어 3개 중 실제로 센서가 달려 측정되는 대표 1개 (교체도 3개를 한꺼번에 하므로
  // 개별 선택은 의미가 없다 - 타이어별 배열이 아니라 단일 객체).
  tire: TireWearResult;
}

export interface HistoryPoint {
  time: string;
  [metric: string]: string | number | null;
}

export interface HistoryResponse {
  machineId: string;
  minutes: number;
  count: number;
  data: HistoryPoint[];
}
