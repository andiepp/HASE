"use strict";

(() => {
  const maximumSources = 16;
  const debounceMilliseconds = 250;
  let timer = null;
  let revision = 0;

  const enumerate = async (scheduledRevision) => {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      if (scheduledRevision !== revision) {
        return;
      }
      const identities = [];
      const seen = new Set();
      for (const device of devices) {
        if (device.kind !== "videoinput" ||
            typeof device.deviceId !== "string" ||
            device.deviceId.length === 0 ||
            seen.has(device.deviceId)) {
          continue;
        }
        seen.add(device.deviceId);
        identities.push({ deviceId: device.deviceId });
      }
      if (identities.length > maximumSources) {
        return;
      }
      window.chrome.webview.postMessage({
        version: 1,
        kind: "inventory",
        devices: identities
      });
    } catch {
      // A failed observation is not an authoritative empty inventory.
    }
  };

  const schedule = () => {
    revision += 1;
    const scheduledRevision = revision;
    if (timer !== null) {
      clearTimeout(timer);
    }
    timer = setTimeout(() => {
      timer = null;
      void enumerate(scheduledRevision);
    }, debounceMilliseconds);
  };

  navigator.mediaDevices.addEventListener("devicechange", schedule);
  window.addEventListener("beforeunload", () => {
    navigator.mediaDevices.removeEventListener("devicechange", schedule);
    if (timer !== null) {
      clearTimeout(timer);
    }
  });
  schedule();
})();
