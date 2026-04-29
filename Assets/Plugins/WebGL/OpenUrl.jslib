mergeInto(LibraryManager.library, {
  // Navigate in the same tab (creates history entry)
  OpenUrlInSameTab: function(urlPtr) {
    var url = UTF8ToString(urlPtr);
    window.location.href = url;
  },

  // Navigate in the same tab but replace current history entry (back won't return)
  OpenUrlReplace: function(urlPtr) {
    var url = UTF8ToString(urlPtr);
    window.location.replace(url);
  },

  // Alternative: open in same tab using window.open with _self
  OpenUrlInSelf: function(urlPtr) {
    var url = UTF8ToString(urlPtr);
    window.open(url, "_self");
  }
});