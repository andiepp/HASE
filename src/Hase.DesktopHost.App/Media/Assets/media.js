"use strict";

(() => {
  const video = document.getElementById("capture");
  const peerConfiguration = Object.freeze({
    iceServers: [],
    iceCandidatePoolSize: 0,
    bundlePolicy: "max-bundle",
    rtcpMuxPolicy: "require"
  });
  let stream = null;
  let peer = null;
  let includeAudio = false;
  let nextOutboundSequence = 1;
  let nextInboundSequence = 1;
  let localDescriptionPublished = false;
  let pendingLocalCandidates = [];
  let pendingRemoteCandidates = [];

  const send = (kind, failureCode) => {
    const message = { version: 1, kind };
    if (failureCode) {
      message.failureCode = failureCode;
    }
    window.chrome.webview.postMessage(message);
  };

  const sendNegotiation = (negotiationKind, sensitivePayload) => {
    window.chrome.webview.postMessage({
      version: 1,
      kind: "negotiation",
      sequence: nextOutboundSequence++,
      negotiationKind,
      sensitivePayload
    });
  };

  const classifyCaptureFailure = (error) => {
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

  const hasRequiredSdp = (sdp, type, audioRequired) => {
    if (typeof sdp !== "string" ||
        !/a=fingerprint:sha-256\s+[0-9A-F:]+/i.test(sdp) ||
        !/a=rtcp-mux\r?$/im.test(sdp) ||
        !/a=rtpmap:\d+\s+VP8\/90000\r?$/im.test(sdp)) {
      return false;
    }
    if (audioRequired &&
        !/a=rtpmap:\d+\s+opus\/48000(?:\/2)?\r?$/im.test(sdp)) {
      return false;
    }
    return type === "offer"
      ? /a=setup:actpass\r?$/im.test(sdp)
      : /a=setup:(active|passive)\r?$/im.test(sdp);
  };

  const setCodecPreference = (transceiver, kind) => {
    if (!transceiver || typeof transceiver.setCodecPreferences !== "function") {
      throw new Error("codec-unsupported");
    }
    const capabilities = RTCRtpSender.getCapabilities(kind);
    const mimeType = kind === "video" ? "video/VP8" : "audio/opus";
    const codecs = capabilities && capabilities.codecs
      ? capabilities.codecs.filter(codec =>
          codec.mimeType.toLowerCase() === mimeType.toLowerCase())
      : [];
    if (codecs.length === 0) {
      throw new Error("codec-unsupported");
    }
    transceiver.setCodecPreferences(codecs);
  };

  const candidatePayload = (candidate) => JSON.stringify({
    candidate: candidate.candidate,
    sdpMid: candidate.sdpMid,
    sdpMLineIndex: candidate.sdpMLineIndex
  });

  const publishLocalCandidate = (candidate) => {
    const item = candidate
      ? ["ice-candidate", candidatePayload(candidate)]
      : ["ice-complete", ""];
    if (!localDescriptionPublished) {
      pendingLocalCandidates.push(item);
      return;
    }
    sendNegotiation(item[0], item[1]);
  };

  const parseCandidatePayload = (payload) => {
    const value = JSON.parse(payload);
    const keys = Object.keys(value).sort().join(",");
    if (keys !== "candidate,sdpMLineIndex,sdpMid" ||
        typeof value.candidate !== "string" ||
        value.candidate.length === 0 ||
        !(value.sdpMid === null || typeof value.sdpMid === "string") ||
        !(value.sdpMLineIndex === null ||
          (Number.isInteger(value.sdpMLineIndex) &&
           value.sdpMLineIndex >= 0))) {
      throw new Error("negotiation-rejected");
    }
    return value;
  };

  const closePeer = () => {
    const closing = peer;
    peer = null;
    localDescriptionPublished = false;
    pendingLocalCandidates = [];
    pendingRemoteCandidates = [];
    if (closing) {
      closing.onicecandidate = null;
      closing.onconnectionstatechange = null;
      closing.close();
    }
  };

  const stop = (notify = true) => {
    closePeer();
    if (stream) {
      for (const track of stream.getTracks()) {
        track.stop();
      }
    }
    stream = null;
    video.srcObject = null;
    includeAudio = false;
    if (notify) {
      send("capture-stopped");
    }
  };

  const createOffer = async () => {
    const current = new RTCPeerConnection(peerConfiguration);
    peer = current;
    nextOutboundSequence = 1;
    nextInboundSequence = 1;
    localDescriptionPublished = false;
    pendingLocalCandidates = [];
    pendingRemoteCandidates = [];

    for (const track of stream.getVideoTracks()) {
      const transceiver = current.addTransceiver(track, {
        direction: "sendonly",
        streams: [stream]
      });
      setCodecPreference(transceiver, "video");
    }
    if (includeAudio) {
      for (const track of stream.getAudioTracks()) {
        const transceiver = current.addTransceiver(track, {
          direction: "sendonly",
          streams: [stream]
        });
        setCodecPreference(transceiver, "audio");
      }
    }

    current.onicecandidate = (event) => {
      if (peer !== current) {
        return;
      }
      publishLocalCandidate(event.candidate);
    };
    current.onconnectionstatechange = () => {
      if (peer !== current) {
        return;
      }
      if (current.connectionState === "connected") {
        send("peer-connected");
      } else if (current.connectionState === "failed") {
        send("peer-faulted", "transport-failed");
        stop(false);
      }
    };

    const offer = await current.createOffer();
    if (!hasRequiredSdp(offer.sdp, "offer", includeAudio)) {
      throw new Error("codec-unsupported");
    }
    await current.setLocalDescription(offer);
    sendNegotiation("offer", current.localDescription.sdp);
    localDescriptionPublished = true;
    for (const item of pendingLocalCandidates) {
      sendNegotiation(item[0], item[1]);
    }
    pendingLocalCandidates = [];
  };

  const start = async (message) => {
    stop(false);
    includeAudio = message.includeAudio;
    const constraints = {
      video: {
        deviceId: { exact: message.videoDeviceId },
        width: { ideal: 640, max: 640 },
        height: { ideal: 480, max: 480 },
        frameRate: { ideal: 30, max: 30 }
      },
      audio: includeAudio
        ? { deviceId: { exact: message.audioDeviceId } }
        : false
    };

    try {
      stream = await navigator.mediaDevices.getUserMedia(constraints);
      video.srcObject = stream;
      send("capture-started");
      await createOffer();
    } catch (error) {
      const failureCode = error && error.message === "codec-unsupported"
        ? "codec-unsupported"
        : classifyCaptureFailure(error);
      stop(false);
      send("capture-faulted", failureCode);
    }
  };

  const applyNegotiation = async (message) => {
    if (!peer || message.sequence !== nextInboundSequence++) {
      throw new Error("negotiation-rejected");
    }
    if (message.negotiationKind === "answer") {
      if (peer.remoteDescription ||
          !hasRequiredSdp(message.sensitivePayload, "answer", includeAudio)) {
        throw new Error("negotiation-rejected");
      }
      await peer.setRemoteDescription({
        type: "answer",
        sdp: message.sensitivePayload
      });
      for (const candidate of pendingRemoteCandidates) {
        await peer.addIceCandidate(candidate);
      }
      pendingRemoteCandidates = [];
      return;
    }
    if (message.negotiationKind === "ice-candidate") {
      const candidate = parseCandidatePayload(message.sensitivePayload);
      if (peer.remoteDescription) {
        await peer.addIceCandidate(candidate);
      } else {
        pendingRemoteCandidates.push(candidate);
      }
      return;
    }
    if (message.negotiationKind === "ice-complete" &&
        message.sensitivePayload === "") {
      if (peer.remoteDescription) {
        await peer.addIceCandidate(null);
      } else {
        pendingRemoteCandidates.push(null);
      }
      return;
    }
    throw new Error("negotiation-rejected");
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
    if (message.kind === "apply-negotiation") {
      void applyNegotiation(message).catch(() => {
        send("peer-faulted", "negotiation-rejected");
        stop(false);
      });
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

  window.addEventListener("beforeunload", () => stop(false));
  send("ready");
})();
