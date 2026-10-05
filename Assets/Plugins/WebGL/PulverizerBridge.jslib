// Unity WebGL -> 브라우저 JS 브리지 구현.
// pulverizer-frontend/src/unity/bridge.ts 의 window.__pulverizerTwinBridge 계약과
// 반드시 이름을 맞춰야 한다 (onPartSelected / onSelectionCleared).
mergeInto(LibraryManager.library, {
  PulverizerTwin_NotifyPartSelected: function (partIdPtr) {
    var partId = UTF8ToString(partIdPtr);
    if (window.__pulverizerTwinBridge && window.__pulverizerTwinBridge.onPartSelected) {
      window.__pulverizerTwinBridge.onPartSelected(partId);
    }
  },

  PulverizerTwin_NotifySelectionCleared: function () {
    if (window.__pulverizerTwinBridge && window.__pulverizerTwinBridge.onSelectionCleared) {
      window.__pulverizerTwinBridge.onSelectionCleared();
    }
  },
});
