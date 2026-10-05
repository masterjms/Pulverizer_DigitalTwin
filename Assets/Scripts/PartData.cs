using UnityEngine;

namespace CoalPulverizer
{
    public sealed class PartData
    {
        public string Id;
        public string KoreanName;
        public string EnglishName;
        public string Description;
        public string[] Specs;
        public int BlueprintZone;       // 도면에서 하이라이트할 구역 인덱스
        public Color HighlightColor;
        public WearData WearData;       // null이면 마모 예측 패널 미표시
    }
}
