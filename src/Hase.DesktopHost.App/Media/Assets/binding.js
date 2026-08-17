"use strict";

(() => {
  const bridge = globalThis.chrome?.webview;
  const status = document.getElementById("status");
  const discoverButton = document.getElementById("discover");
  const discoverAudio = document.getElementById("discoverAudio");
  const selection = document.getElementById("selection");
  const video = document.getElementById("video");
  const audio = document.getElementById("audio");
  const save = document.getElementById("save");
  const cancel = document.getElementById("cancel");
  let activeStream = null;
  let discoveryPending = false;

  const post = (message) => bridge?.postMessage({ version: 1, ...message });

  const stopTracks = () => {
    if (activeStream) {
      for (const track of activeStream.getTracks()) {
        track.stop();
      }
      activeStream = null;
    }
  };

  const option = (device, index, role) => {
    const item = document.createElement("option");
    item.value = device.deviceId;
    item.textContent = device.label || `${role} ${index + 1}`;
    return item;
  };

  const faultCode = (error) => {
    if (error?.name === "NotAllowedError" ||
        error?.name === "SecurityError") {
      return "permission-denied";
    }
    if (error?.name === "NotFoundError" ||
        error?.name === "OverconstrainedError") {
      return "device-unavailable";
    }
    return "enumeration-failed";
  };

  const enumerate = async () => {
    try {
      activeStream = await navigator.mediaDevices.getUserMedia({
        video: true,
        audio: discoverAudio.checked
      });
      const devices = await navigator.mediaDevices.enumerateDevices();
      stopTracks();

      const cameras = devices.filter((item) => item.kind === "videoinput");
      const microphones = devices.filter((item) => item.kind === "audioinput");
      if (cameras.length === 0) {
        throw new DOMException("No camera is available.", "NotFoundError");
      }

      video.replaceChildren(...cameras.map(
        (item, index) => option(item, index, "Camera")));
      audio.replaceChildren(new Option("No microphone", ""));
      for (const [index, microphone] of microphones.entries()) {
        audio.append(option(microphone, index, "Microphone"));
      }
      selection.hidden = false;
      status.textContent = "Select one camera and an optional microphone.";
    } catch (error) {
      stopTracks();
      selection.hidden = true;
      status.textContent = "Device discovery failed. No candidate was written.";
      post({ kind: "faulted", failureCode: faultCode(error) });
    } finally {
      discoveryPending = false;
      discoverButton.disabled = false;
    }
  };

  discoverButton.addEventListener("click", () => {
    if (discoveryPending) {
      return;
    }
    discoveryPending = true;
    discoverButton.disabled = true;
    selection.hidden = true;
    status.textContent = "Waiting for explicit local device permission...";
    post({ kind: "discovery-requested" });
  });

  save.addEventListener("click", () => {
    stopTracks();
    if (!video.value) {
      return;
    }
    post({
      kind: "selection-confirmed",
      videoDeviceId: video.value,
      audioDeviceId: audio.value || null
    });
    save.disabled = true;
    discoverButton.disabled = true;
    status.textContent = "Writing the protected candidate...";
  });

  cancel.addEventListener("click", () => {
    stopTracks();
    post({ kind: "cancelled" });
  });

  bridge?.addEventListener("message", (event) => {
    const message = event.data;
    if (message?.version === 1 &&
        message?.kind === "discovery-authorized" &&
        discoveryPending) {
      void enumerate();
    }
    if (message?.version === 1 && message?.kind === "stop-discovery") {
      stopTracks();
    }
  });

  globalThis.addEventListener("pagehide", stopTracks);
  post({ kind: "ready" });
})();
