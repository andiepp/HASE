"use strict";

(() => {
  const video = document.getElementById("capture");
  let stream = null;

  const send = (kind, failureCode) => {
    const message = { version: 1, kind };
    if (failureCode) {
      message.failureCode = failureCode;
    }
    window.chrome.webview.postMessage(message);
  };

  const stop = (notify = true) => {
    if (stream) {
      for (const track of stream.getTracks()) {
        track.stop();
      }
    }
    stream = null;
    video.srcObject = null;
    if (notify) {
      send("capture-stopped");
    }
  };

  const classifyFailure = (error) => {
    switch (error && error.name) {
      case "NotAllowedError":
      case "SecurityError":
        return "permission-denied";
      case "NotFoundError":
        return "device-unavailable";
      case "NotReadableError":
        return "device-busy";
      case "OverconstrainedError":
        return "constraint-rejected";
      default:
        return "browser-failed";
    }
  };

  const start = async (message) => {
    stop(false);
    const constraints = {
      video: {
        deviceId: { exact: message.videoDeviceId },
        width: { ideal: 640, max: 640 },
        height: { ideal: 480, max: 480 },
        frameRate: { ideal: 30, max: 30 }
      },
      audio: message.includeAudio
        ? { deviceId: { exact: message.audioDeviceId } }
        : false
    };

    try {
      stream = await navigator.mediaDevices.getUserMedia(constraints);
      video.srcObject = stream;
      send("capture-started");
    } catch (error) {
      stop(false);
      send("capture-faulted", classifyFailure(error));
    }
  };

  window.chrome.webview.addEventListener("message", (event) => {
    const message = event.data;
    if (!message || message.version !== 1 || typeof message.kind !== "string") {
      return;
    }

    if (message.kind === "stop-capture") {
      stop();
      return;
    }

    if (message.kind !== "start-capture" ||
        typeof message.videoDeviceId !== "string" ||
        message.videoDeviceId.length === 0 ||
        typeof message.includeAudio !== "boolean" ||
        (message.includeAudio &&
         (typeof message.audioDeviceId !== "string" ||
          message.audioDeviceId.length === 0))) {
      return;
    }

    void start(message);
  });

  window.addEventListener("beforeunload", stop);
  send("ready");
})();
