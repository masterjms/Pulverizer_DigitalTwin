using System.Runtime.InteropServices;
using UnityEngine;

namespace CoalPulverizer
{
    // Unity(WebGL 빌드) -> React 이벤트 전달.
    // 실제 함수 구현은 Assets/Plugins/WebGL/PulverizerBridge.jslib 에 있고,
    // 브라우저 쪽 계약은 pulverizer-frontend/src/unity/bridge.ts 의
    // window.__pulverizerTwinBridge 와 반드시 이름을 맞춰야 한다.
    // 에디터/다른 빌드 타겟에서는 콘솔 로그만 남기고 조용히 무시한다.
    public static class WebGLBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PulverizerTwin_NotifyPartSelected(string partId);

        [DllImport("__Internal")]
        private static extern void PulverizerTwin_NotifySelectionCleared();
#endif

        public static void NotifyPartSelected(string partId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PulverizerTwin_NotifyPartSelected(partId);
#else
            Debug.Log($"[WebGLBridge] (non-WebGL) partSelected: {partId}");
#endif
        }

        public static void NotifySelectionCleared()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PulverizerTwin_NotifySelectionCleared();
#else
            Debug.Log("[WebGLBridge] (non-WebGL) selectionCleared");
#endif
        }
    }
}
