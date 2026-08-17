"use strict";

(() => {
  const bridge = globalThis.chrome?.webview;
  const status = document.getElementById("status");
  const discoverButton = document.getElementById("discover");
  const discoverAudio = document.getElementById("discoverAudio");
  const selection = document.getElementById("selection");
  const preview = document.getElementById("preview");
  const cameraChoices = document.getElementById("cameraChoices");
  const selectedCameraCount = document.getElementById("selectedCameraCount");
  const audio = document.getElementById("audio");
  const save = document.getElementById("save");
  const cancel = document.getElementById("cancel");
  const selectedVideoDeviceIds = new Set();
  let availableCameras = [];
  let activeStream = null;
  let previewDeviceId = null;
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
    previewDeviceId = null;
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

  const updateSelectionState = () => {
    const count = selectedVideoDeviceIds.size;
    selectedCameraCount.textContent = `Selected cameras: ${count}`;
    save.disabled = count < 1 || count > 16;
  };

  const setCameraChoicesDisabled = (disabled) => {
    for (const checkbox of cameraChoices.querySelectorAll(
      'input[type="checkbox"]')) {
      checkbox.disabled = disabled;
    }
  };

  const previewCamera = async (deviceId) => {
    stopTracks();
    const revision = operationRevision;
    setCameraChoicesDisabled(true);
    save.disabled = true;
    status.textContent = "Opening the selected camera preview...";
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { deviceId: { exact: deviceId } },
        audio: false
      });
      if (revision !== operationRevision) {
        releaseStream(stream);
        return;
      }
      activeStream = stream;
      previewDeviceId = deviceId;
      await showPreview(activeStream);
      if (revision !== operationRevision) {
        return;
      }
      status.textContent =
        "Previewing the checked camera. Check every camera to include.";
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
        setCameraChoicesDisabled(false);
        updateSelectionState();
      }
    }
  };

  const handleCameraChoiceChanged = (event) => {
    const checkbox = event.currentTarget;
    const deviceId = checkbox.value;
    if (checkbox.checked) {
      selectedVideoDeviceIds.add(deviceId);
      updateSelectionState();
      void previewCamera(deviceId);
      return;
    }

    selectedVideoDeviceIds.delete(deviceId);
    updateSelectionState();
    if (previewDeviceId === deviceId) {
      const remainingDeviceId = availableCameras
        .find((camera) => selectedVideoDeviceIds.has(camera.deviceId))
        ?.deviceId;
      if (remainingDeviceId) {
        void previewCamera(remainingDeviceId);
      } else {
        stopTracks();
        status.textContent = "Check at least one camera.";
      }
    }
  };

  const renderCameraChoices = (cameras, initiallySelectedDeviceId) => {
    cameraChoices.replaceChildren();
    selectedVideoDeviceIds.clear();

    for (const [index, camera] of cameras.entries()) {
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.value = camera.deviceId;
      checkbox.id = `camera-choice-${index + 1}`;
      checkbox.checked = camera.deviceId === initiallySelectedDeviceId;
      if (checkbox.checked) {
        selectedVideoDeviceIds.add(camera.deviceId);
      }
      checkbox.addEventListener("change", handleCameraChoiceChanged);

      const label = document.createElement("label");
      label.className = "camera-choice";
      label.htmlFor = checkbox.id;
      label.append(
        checkbox,
        document.createTextNode(
          camera.label || `Camera ${index + 1}`));
      cameraChoices.append(label);
    }

    updateSelectionState();
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

      availableCameras =
        devices.filter((item) => item.kind === "videoinput");
      const microphones = discoverAudio.checked
        ? devices.filter((item) => item.kind === "audioinput")
        : [];
      if (availableCameras.length === 0) {
        throw new DOMException("No camera is available.", "NotFoundError");
      }

      const activeVideoDeviceId = activeStream
        .getVideoTracks()[0]
        ?.getSettings()
        ?.deviceId;
      const initiallySelectedDeviceId = availableCameras.some(
        (item) => item.deviceId === activeVideoDeviceId)
        ? activeVideoDeviceId
        : availableCameras[0].deviceId;
      renderCameraChoices(availableCameras, initiallySelectedDeviceId);

      audio.replaceChildren(new Option("No microphone", ""));
      for (const [index, microphone] of microphones.entries()) {
        audio.append(option(microphone, index, "Microphone"));
      }
      audio.value = "";
      audio.disabled = !discoverAudio.checked;

      for (const track of activeStream.getAudioTracks()) {
        track.stop();
        activeStream.removeTrack(track);
      }
      previewDeviceId = initiallySelectedDeviceId;
      selection.hidden = false;
      await showPreview(activeStream);
      if (revision !== operationRevision) {
        return;
      }
      status.textContent =
        "Previewing the checked camera. Check every camera to include.";
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
    availableCameras = [];
    selectedVideoDeviceIds.clear();
    cameraChoices.replaceChildren();
    updateSelectionState();
    audio.replaceChildren(new Option("No microphone", ""));
    audio.value = "";
    audio.disabled = true;
    discoverButton.disabled = true;
    selection.hidden = true;
    status.textContent = "Waiting for explicit local device permission...";
    post({ kind: "discovery-requested" });
  });

  save.addEventListener("click", () => {
    const selectedCameras = availableCameras
      .filter((camera) => selectedVideoDeviceIds.has(camera.deviceId));
    stopTracks();
    if (selectedCameras.length === 0 || selectedCameras.length > 16) {
      return;
    }
    post({
      kind: "selection-confirmed",
      selections: selectedCameras.map((camera) => ({
        videoDeviceId: camera.deviceId,
        audioDeviceId: audio.value || null
      }))
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
  updateSelectionState();
  post({ kind: "ready" });
})();
