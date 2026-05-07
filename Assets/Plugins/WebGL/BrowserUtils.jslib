mergeInto(LibraryManager.library, {
  ReloadPage: function () {
    window.location.reload();
  },

  CopyTextToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function (err) {
        console.error("Clipboard copy failed:", err);
      });
    } else {
      var textarea = document.createElement("textarea");
      textarea.value = text;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();

      try {
        document.execCommand("copy");
      } catch (err) {
        console.error("Fallback clipboard copy failed:", err);
      }

      document.body.removeChild(textarea);
    }
  }
});