"use strict";

(() => {
  const video = document.getElementById("presentation");
  const peerConfiguration = Object.freeze({
    iceServers: [],
    iceCandidatePoolSize: 0,
    bundlePolicy: "max-bundle",
    rtcpMuxPolicy: "require"
  });
  let peer = null;
  let remoteStream = null;
  let includeAudio = false;
  let nextOutboundSequence = 1;
  let nextInboundSequence = 1;
  let localDescriptionPublished = false;
  let pendingLocalCandidates = [];
  let pendingRemoteCandidates = [];
  let presentationStarted = false;

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
    if (!audioRequired && /m=audio\s/im.test(sdp)) {
      return false;
    }
    return type === "offer"
      ? /a=setup:actpass\r?$/im.test(sdp)
      : /a=setup:(active|passive)\r?$/im.test(sdp);
  };

  const setCodecPreference = (transceiver) => {
    if (!transceiver || typeof transceiver.setCodecPreferences !== "function") {
      throw new Error("codec-unsupported");
    }
    const kind = transceiver.receiver.track.kind;
    const capabilities = RTCRtpReceiver.getCapabilities(kind);
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

  const clear = (notify = true) => {
    const closing = peer;
    peer = null;
    localDescriptionPublished = false;
    pendingLocalCandidates = [];
    pendingRemoteCandidates = [];
    if (closing) {
      closing.onicecandidate = null;
      closing.ontrack = null;
      closing.onconnectionstatechange = null;
      closing.close();
    }
    if (remoteStream) {
      for (const track of remoteStream.getTracks()) {
        track.stop();
      }
    }
    remoteStream = null;
    presentationStarted = false;
    video.pause();
    video.srcObject = null;
    if (notify) {
      send("presentation-stopped");
    }
  };

  const begin = (message) => {
    clear(false);
    includeAudio = message.includeAudio;
    nextOutboundSequence = 1;
    nextInboundSequence = 1;
    localDescriptionPublished = false;
    pendingLocalCandidates = [];
    pendingRemoteCandidates = [];
    remoteStream = new MediaStream();
    const current = new RTCPeerConnection(peerConfiguration);
    peer = current;

    current.onicecandidate = (event) => {
      if (peer !== current) {
        return;
      }
      publishLocalCandidate(event.candidate);
    };
    current.ontrack = (event) => {
      if (peer !== current ||
          (event.track.kind === "audio" && !includeAudio)) {
        return;
      }
      remoteStream.addTrack(event.track);
      video.srcObject = remoteStream;
      if (!presentationStarted) {
        void video.play().then(() => {
          if (peer === current && !presentationStarted) {
            presentationStarted = true;
            send("presentation-started");
          }
        }).catch(() => send("presentation-faulted", "playback-blocked"));
      }
    };
    current.onconnectionstatechange = () => {
      if (peer === current && current.connectionState === "connected") {
        send("peer-connected");
      } else if (peer === current && current.connectionState === "failed") {
        send("presentation-faulted", "transport-unavailable");
        clear(false);
      }
    };
  };

  const applyNegotiation = async (message) => {
    if (!peer || message.sequence !== nextInboundSequence++) {
      throw new Error("negotiation-rejected");
    }
    if (message.negotiationKind === "offer") {
      if (peer.remoteDescription ||
          !hasRequiredSdp(message.sensitivePayload, "offer", includeAudio)) {
        throw new Error("negotiation-rejected");
      }
      await peer.setRemoteDescription({
        type: "offer",
        sdp: message.sensitivePayload
      });
      for (const transceiver of peer.getTransceivers()) {
        if (transceiver.receiver.track.kind === "audio" && !includeAudio) {
          throw new Error("negotiation-rejected");
        }
        transceiver.direction = "recvonly";
        setCodecPreference(transceiver);
      }
      for (const candidate of pendingRemoteCandidates) {
        await peer.addIceCandidate(candidate);
      }
      pendingRemoteCandidates = [];
      const answer = await peer.createAnswer();
      if (!hasRequiredSdp(answer.sdp, "answer", includeAudio)) {
        throw new Error("codec-unsupported");
      }
      await peer.setLocalDescription(answer);
      sendNegotiation("answer", peer.localDescription.sdp);
      localDescriptionPublished = true;
      for (const item of pendingLocalCandidates) {
        sendNegotiation(item[0], item[1]);
      }
      pendingLocalCandidates = [];
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
    if (message.kind === "clear-presentation") {
      clear();
      return;
    }
    if (message.kind === "begin-presentation" &&
        typeof message.includeAudio === "boolean") {
      begin(message);
      return;
    }
    if (message.kind === "apply-negotiation") {
      void applyNegotiation(message).catch((error) => {
        const code = error && error.message === "codec-unsupported"
          ? "codec-unsupported"
          : "negotiation-rejected";
        send("presentation-faulted", code);
        clear(false);
      });
    }
  });

  window.addEventListener("beforeunload", () => clear(false));
  send("ready");
})();
