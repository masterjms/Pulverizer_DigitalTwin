using UnityEngine;

namespace CoalPulverizer.UI
{
    // 한국어 지원 폰트를 OS 시스템 폰트에서 로드 (WebGL 빌드 시 별도 처리 필요)
    internal static class UIFonts
    {
        private static Font _korean;

        internal static Font Korean
        {
            get
            {
                if (_korean != null) return _korean;
                // macOS: Apple SD Gothic Neo / AppleGothic
                // Windows: Malgun Gothic / Gulim
                _korean = Font.CreateDynamicFontFromOSFont(
                    new[] { "Apple SD Gothic Neo", "AppleGothic", "Malgun Gothic",
                             "Gulim", "NanumGothic", "Arial Unicode MS" }, 12);
                _korean ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _korean;
            }
        }
    }
}
