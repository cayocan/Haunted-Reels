mergeInto(LibraryManager.library, {

  JS_PostSessionData: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    try {
      var data = JSON.parse(json);
      if (window.parent && window.parent !== window) {
        window.parent.postMessage(data, "*");
      }
      window.dispatchEvent(new CustomEvent("haunted-reels-session", { detail: data }));
    } catch (e) {
      console.warn("[SlotBridge] JS_PostSessionData parse error:", e);
    }
  },

});
