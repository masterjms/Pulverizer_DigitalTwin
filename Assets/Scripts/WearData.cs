using UnityEngine;

namespace CoalPulverizer
{
    // Archard 기반 기계적 마모 지수 (AMWI) 데이터
    // W ∝ F_N · v · C_abrasive
    // AMWI(t) = GP(t) × CT(t) × BI(t) × RL(t)
    public sealed class WearData
    {
        public float GP;   // 가압 하중 인자: (P̂_J + P̂_H) / 2  — 저널·유압 압력 평균
        public float CT;   // 연삭재 유입 인자: Q̂(t)             — 급탄율 정규화
        public float BI;   // 입자 체류 인자: ΔP̂(t)              — Bowl 차압 정규화
        public float RL;   // 재순환 마찰 인자: (N̂_C + Ĉ_ctrl)/2 — 분류기 속도 정규화

        public float WornMm;           // 현재 누적 마모량 (mm)
        public float WearLimitMm;      // 교체 기준 마모 한계 (mm)
        public float DailyWearRateMm;  // 일일 마모율 (mm/day) — 현재 AMWI 기반 추정

        // 파생 프로퍼티
        public float AMWI         => GP * CT * BI * RL;
        public float WearPercent  => Mathf.Clamp01(WornMm / WearLimitMm);
        public int   RemainingDays => DailyWearRateMm > 0f
            ? Mathf.Max(0, Mathf.RoundToInt((WearLimitMm - WornMm) / DailyWearRateMm))
            : 9999;

        public WearSeverity Severity
        {
            get
            {
                float v = AMWI;
                if (v > 0.60f) return WearSeverity.Alert;
                if (v > 0.30f) return WearSeverity.Warning;
                return WearSeverity.Normal;
            }
        }
    }

    public enum WearSeverity { Normal, Warning, Alert }
}
