"use strict";

(() => {
  const video = document.getElementById("presentation");

  const send = (kind) => {
    window.chrome.webview.postMessage({ version: 1, kind });
  };

  const clear = () => {
    video.pause();
    video.srcObject = null;
    send("presentation-stopped");
  };

  window.chrome.webview.addEventListener("message", (event) => {
    const message = event.data;
    if (message && message.version === 1 &&
        message.kind === "clear-presentation") {
      clear();
    }
  });

  window.addEventListener("beforeunload", clear);
  send("ready");
})();
