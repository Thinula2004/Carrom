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
  },

  EnterFullscreen: function () {
    var canvas = document.querySelector("#unity-canvas");

    if (!canvas) {
      canvas = document.querySelector("canvas");
    }

    if (!canvas) {
      console.warn("EnterFullscreen failed: canvas not found.");
      return;
    }

    var requestFullscreen =
      canvas.requestFullscreen ||
      canvas.webkitRequestFullscreen ||
      canvas.mozRequestFullScreen ||
      canvas.msRequestFullscreen;

    if (requestFullscreen) {
      requestFullscreen.call(canvas).catch(function (err) {
        console.error("EnterFullscreen failed:", err);
      });
    } else {
      console.warn("Fullscreen API is not supported.");
    }
  },

  ExitFullscreen: function () {
    var exitFullscreen =
      document.exitFullscreen ||
      document.webkitExitFullscreen ||
      document.mozCancelFullScreen ||
      document.msExitFullscreen;

    var fullscreenElement =
      document.fullscreenElement ||
      document.webkitFullscreenElement ||
      document.mozFullScreenElement ||
      document.msFullscreenElement;

    if (!fullscreenElement) {
      console.log("ExitFullscreen ignored: not currently fullscreen.");
      return;
    }

    if (exitFullscreen) {
      exitFullscreen.call(document).catch(function (err) {
        console.error("ExitFullscreen failed:", err);
      });
    } else {
      console.warn("Exit fullscreen API is not supported.");
    }
  }
});