mergeInto(LibraryManager.library, {
  NextroTwinEvent: function (json) {
    var value = JSON.parse(UTF8ToString(json));
    window.dispatchEvent(new CustomEvent('nextro-twin', { detail: value }));
  }
});
