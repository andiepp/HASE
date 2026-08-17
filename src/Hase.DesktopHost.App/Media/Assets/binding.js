"use strict";

(() => {
  const bridge = globalThis.chrome?.webview;
  const status = document.getElementById("status");
  const discoverButton = document.getElementById("discover");
  const discoverAudio = document.getElementById("discoverAudio");
  const selection = document.getElementById("selection");
  const preview = document.getElementById("preview");
  const video = document.getElementById("video");
  const audio = document.getElementById("audio");
  const save = document.getElementById("save");
  const cancel = document.getElementById("cancel");
  let activeStream = null;
  let discoveryPending = false;
  let operationRevision = 0;

  const post = (message) => bridge?.postMessage({ version: 1, ...message });

  const releaseStream = (stream) => {
    if (stream) {
      for (const track of stream.getTracks()) {
        track.stop();
      }
    }
  };

  const stopTracks = () => {
    operationRevision += 1;
    preview.pause();
    preview.srcObject = null;
    if (activeStream) {
      releaseStream(activeStream);
      activeStream = null;
    }
  };

  const showPreview = async (stream) => {
    preview.srcObject = stream;
    await preview.play();
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
    const revision = ++operationRevision;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: true,
        audio: discoverAudio.checked
      });
      if (revision !== operationRevision) {
        releaseStream(stream);
        return;
      }
      activeStream = stream;
      const devices = await navigator.mediaDevices.enumerateDevices();
      if (revision !== operationRevision) {
        return;
      }

      const cameras = devices.filter((item) => item.kind === "videoinput");
      const microphones = devices.filter((item) => item.kind === "audioinput");
      if (cameras.length === 0) {
        throw new DOMException("No camera is available.", "NotFoundError");
      }

      video.replaceChildren(...cameras.map(
        (item, index) => option(item, index, "Camera")));
      const activeVideoDeviceId = activeStream
        .getVideoTracks()[0]
        ?.getSettings()
        ?.deviceId;
      if (cameras.some((item) => item.deviceId === activeVideoDeviceId)) {
        video.value = activeVideoDeviceId;
      }
      audio.replaceChildren(new Option("No microphone", ""));
      for (const [index, microphone] of microphones.entries()) {
        audio.append(option(microphone, index, "Microphone"));
      }
      for (const track of activeStream.getAudioTracks()) {
        track.stop();
        activeStream.removeTrack(track);
      }
      selection.hidden = false;
      await showPreview(activeStream);
      if (revision !== operationRevision) {
        return;
      }
      status.textContent =
        "Previewing the selected camera. Select an optional microphone.";
    } catch (error) {
      if (revision !== operationRevision) {
        return;
      }
      stopTracks();
      discoveryPending = false;
      discoverButton.disabled = false;
      selection.hidden = true;
      status.textContent = "Device discovery failed. No candidate was written.";
      post({ kind: "faulted", failureCode: faultCode(error) });
    } finally {
      if (revision === operationRevision) {
        discoveryPending = false;
        discoverButton.disabled = false;
      }
    }
  };

  discoverButton.addEventListener("click", () => {
    if (discoveryPending) {
      return;
    }
    discoveryPending = true;
    stopTracks();
    discoverButton.disabled = true;
    selection.hidden = true;
    status.textContent = "Waiting for explicit local device permission...";
    post({ kind: "discovery-requested" });
  });

  video.addEventListener("change", async () => {
    const selectedVideoDeviceId = video.value;
    stopTracks();
    const revision = operationRevision;
    if (!selectedVideoDeviceId) {
      return;
    }

    video.disabled = true;
    save.disabled = true;
    status.textContent = "Opening the selected camera preview...";
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { deviceId: { exact: selectedVideoDeviceId } },
        audio: false
      });
      if (revision !== operationRevision) {
        releaseStream(stream);
        return;
      }
      activeStream = stream;
      await showPreview(activeStream);
      if (revision !== operationRevision) {
        return;
      }
      status.textContent =
        "Previewing the selected camera. Select an optional microphone.";
    } catch (error) {
      if (revision !== operationRevision) {
        return;
      }
      stopTracks();
      selection.hidden = true;
      status.textContent =
        "Camera preview failed. No candidate was written; discover devices again.";
      post({ kind: "faulted", failureCode: faultCode(error) });
    } finally {
      if (revision === operationRevision) {
        video.disabled = false;
        save.disabled = false;
      }
    }
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
