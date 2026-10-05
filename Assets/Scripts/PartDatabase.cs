using System.Collections.Generic;
using UnityEngine;

namespace CoalPulverizer
{
    // pulverizer 프로젝트의 11개 부품 중 이번 단계 범위(타이어 3개 + 하우징 + 볼 테이블)만
    // 우선 이식한 버전이다. 나머지 8개 부품(분급기·배관·구동계 등)은 다음 단계에서 추가한다.
    //
    // pulverizer에는 GameObject 이름이 런타임 생성 문장형이라 이름 접두사로 PartId를
    // 추론하는 FindIdByObjectName()이 필요했지만, 이 프로젝트는 PartTag를 직접 부착하므로
    // 그 매핑 테이블은 가져오지 않는다.
    public static class PartDatabase
    {
        private static readonly Dictionary<string, PartData> _byId;

        static PartDatabase()
        {
            _byId = new Dictionary<string, PartData>
            {
                ["housing"] = new PartData
                {
                    Id = "housing",
                    KoreanName = "압력 하우징",
                    EnglishName = "Pressure Housing Shell",
                    Description = "1차 공기와 미분탄 흐름을 밀폐하는 원통형 압력 용기입니다. 절개형 구조로 내부 점검이 가능하며 상하 플랜지 볼트로 결합됩니다.",
                    Specs = new[]
                    {
                        "설계 압력: 약 +3 kPa (내압)",
                        "재질: 탄소강 (두께 약 20 mm)",
                        "플랜지 볼트: 상·하 각 28개",
                        "점검구: 측면 접근 도어 1개",
                        "절개 각도: 320° 가시범위",
                    },
                    BlueprintZone = 7,
                    HighlightColor = new Color(0.7f, 0.75f, 0.78f),
                },
                ["bowl_table"] = new PartData
                {
                    Id = "bowl_table",
                    KoreanName = "볼 테이블 (그라인딩 볼)",
                    EnglishName = "Bowl / Grinding Table",
                    Description = "기어 감속기에 의해 회전하는 주요 분쇄 플레이트입니다. 원료탄이 테이블 위에 낙하하면 원심력으로 외곽으로 이동하며 롤 타이어와 불 링 사이에서 분쇄됩니다.",
                    Specs = new[]
                    {
                        "회전 속도: 약 45 RPM",
                        "재질: 고망간강 + 세라믹 내마모 라이너",
                        "직경: 약 3.2 m (B호기 기준)",
                        "구동: 수직 기어 감속기를 통한 전동기 직결",
                    },
                    BlueprintZone = 2,
                    HighlightColor = new Color(0.8f, 0.1f, 0.1f),
                },

                ["tire_01"] = MakeTire(1),
                ["tire_02"] = MakeTire(2),
                ["tire_03"] = MakeTire(3),
            };
        }

        // 롤 타이어 3개는 pulverizer의 단일 "roll_assembly" 항목을 개별 인스턴스로 확장한 것이다.
        // 실측 마모 데이터가 아직 없으므로, 세 타이어 모두 동일한 샘플값을 임시로 사용한다.
        // TODO: 실제 타이어별 센서/점검 데이터가 확보되면 WearData를 타이어별로 갱신할 것.
        private static PartData MakeTire(int index)
        {
            return new PartData
            {
                Id = $"tire_0{index}",
                KoreanName = $"롤 타이어 어셈블리 #{index}",
                EnglishName = $"Roll Wheel Assembly #{index}",
                Description = "롤 타이어가 불 링 위를 눌러 석탄을 분쇄합니다. 유압 스프링으로 가압되며, 저널 샤프트와 하우징이 하중을 지지합니다. (120° 간격으로 3개 배치)",
                Specs = new[]
                {
                    "재질: 고크롬 내마모강",
                    "마모 한계: 50 mm",
                    "교체 주기: ~5,000 운전시간",
                    "가압 방식: 유압 스프링",
                },
                BlueprintZone = 4,
                HighlightColor = new Color(1f, 0.3f, 0.5f),
                // AMWI 샘플 센서값 (실측 연동 전 placeholder, B호기 2026-07-14 샘플 기준)
                WearData = new WearData
                {
                    GP = 0.72f,
                    CT = 0.85f,
                    BI = 0.61f,
                    RL = 0.78f,
                    WornMm          = 23.4f,
                    WearLimitMm     = 50.0f,
                    DailyWearRateMm = 0.041f,
                },
            };
        }

        public static PartData GetById(string id)
        {
            _byId.TryGetValue(id, out var data);
            return data;
        }
    }
}
