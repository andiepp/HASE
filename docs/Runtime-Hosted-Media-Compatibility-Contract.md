# Runtime-Hosted Media Compatibility Contract

This document is the normative Increment 55A compatibility boundary for
[ADR-0055](adr/ADR-0055-Runtime-Hosted-Live-Video-and-Audio.md). It constrains
later implementation increments; it does not itself authorize implementation,
device access, media capture, deployment, or physical validation.

## Initial supported topology

```text
one configured Windows Runtime Host camera
  + zero or one associated Runtime Host microphone
  -> one authenticated HASE Client profile
  -> one view-only WPF Client session
```

- Video direction is Runtime Host send-only and Client receive-only.
- Audio direction, when separately requested and authorized, is Runtime Host
  send-only and Client receive-only.
- Only one active media session is supported application-wide.
- No camera or microphone on the Viewing Client is opened.
- No ESP32 endpoint or HASE instrument is a media source.

## Platform baseline

The initial implementation targets Windows x64 and the repository's .NET 10
WPF/Prism 9 application baseline. Microsoft Edge WebView2 is the selected
capture, WebRTC, decode, and presentation boundary.

Read-only 55A readiness established:

| Role | Computer | Operating system | WebView2 Runtime | Relevant result |
| --- | --- | --- | --- | --- |
| Runtime Host | AEPRAKETE | Windows 10 Pro 10.0.19045 x64 | 151.0.4129.86 | One enabled camera-class device; two enabled microphone endpoints; privacy preflight not denied |
| Viewing Client | LTAEP | Windows 11 Pro 10.0.26200 x64 | 151.0.4129.86 | Private-network and presentation prerequisites ready; privacy preflight not denied |

Both machines have a .NET 10 SDK and Windows Desktop Runtime. The repository
does not reference WebView2 at the accepted baseline. A later increment must
pin and validate the SDK, assets, runtime detection, and application packaging.
The observed Evergreen Runtime version is readiness evidence, not a permanently
pinned runtime requirement.

Device counts only establish discoverable enabled device classes. They do not
prove a particular source, driver, capture format, frame rate, audio format, or
end-to-end stream.

## Source identity and configuration

A source is identified remotely by:

```text
RuntimeHostId + MediaSourceId + MediaSourceGeneration
```

- `RuntimeHostId` is the existing authoritative Runtime Host identity.
- `MediaSourceId` is a stable, sanitized logical identifier from local Runtime
  Host configuration.
- `MediaSourceGeneration` changes when the configured binding or authoritative
  availability is recreated.
- The exact Windows device identifier and friendly name are local configuration
  data and are not accepted from or disclosed to the Client.
- A Start request for a stale generation is rejected before device opening.
- Missing, disabled, busy, or incompatible devices make the source unavailable
  or fault the requested session without selecting a different device.

No automatic camera or microphone selection is part of the initial contract.
Configuration publication and deployment require a later explicit increment.

## Plane separation

### Control plane

The existing HASE gRPC listener and mutual-TLS identity boundary carry only:

- sanitized source capabilities;
- authorization decisions;
- explicit Start and Stop operations;
- bounded, ordered WebRTC negotiation messages;
- sanitized session state and terminal reason; and
- session lease and cleanup signals.

The media control contract is separately versioned. It does not alter or reuse
the endpoint, Property, Command, Event, observation, diagnostics, or ESP32
Protocol Version 1 data model.

### Media plane

Continuous video and audio use direct WebRTC between the Runtime Host and
Viewing Client over an operator-managed private network. WebRTC DTLS-SRTP
encryption is mandatory. Failure to establish the encrypted plane ends the
session; there is no downgrade to plaintext, ordinary gRPC streaming, HASE
diagnostics, or endpoint payloads.

The initial contract has no STUN server, TURN server, cloud signaling, public
relay, public-internet exposure, multicast, or broadcast. ICE candidates are
exchanged only through the authenticated control plane and remain sensitive.

## Capability and authorization contract

Increment 55B fixes the independent protobuf package
`hase.runtime.media.v1`, C# namespace `Hase.Runtime.Media.Grpc.V1`, and service
`RuntimeHostMediaControl`. The service has exactly five unary operations:

1. `GetMediaCapabilities` reads sanitized media capabilities and the applicable
   limits;
2. `StartMediaSession` starts against an exact `MediaSourceTarget` generation
   and an explicit `include_audio` value;
3. `ExchangeMediaNegotiation` submits at most one sequenced message,
   acknowledges prior delivery, and returns a bounded pending batch;
4. `GetMediaSessionStatus` reads the sanitized caller-owned session snapshot;
   and
5. `StopMediaSession` idempotently stops the authenticated caller's session.

The contract publishes only logical source identity, generation, availability,
the VP8/Opus allowlist, lifecycle state, sanitized terminal reason, timestamps,
and aggregate counters. Windows device identifiers, friendly names, network
addresses, SDP, ICE, credentials, tokens, and media content are never
capability or status fields. Negotiation payloads exist only in the dedicated
exchange messages and retain their sensitive classification.

The planned authorization vocabulary is:

```text
media.capability.read
media.video.receive
media.audio.receive
media.session.start
media.session.negotiate
media.session.stop
```

Every operation is default-deny and evaluated against the authenticated
principal. Video permission does not imply audio permission. Requesting audio
without source support or exact audio permission fails without opening the
microphone. A session token never replaces principal authentication or action
authorization.

Existing authorization actions, policies, credentials, certificates, profile
selection, target selection, shortcuts, and trust behavior remain unchanged.
55B defines the six permission values but adds no grant to any policy. Start
requires video receive plus session start; audio Start additionally requires
the independent audio receive permission. Negotiation, status, and Stop also
require exact session ownership in addition to their operation permissions.

## Session lifecycle

The externally meaningful states are:

```text
Unavailable
Idle
Starting
Negotiating
Streaming
Stopping
Ended
Faulted
```

Required behavior:

- capability discovery leaves the source closed;
- Start follows one explicit Client action and is never implicit in profile
  connection, discovery, authorization, selection, or recovery;
- one current source generation and one authenticated principal own a session;
- negotiation is ordered, bounded, time-limited, and session-specific;
- Stop is explicit and idempotent;
- a second viewer or session is rejected without disturbing the first;
- disconnect, lease expiry, timeout, source loss, WebView2 failure, application
  shutdown, or terminal protocol error stops media and releases devices; and
- restart and ordinary HASE reconnect return to stopped state and never replay
  or resume Start, negotiation, or Stop.

A Client that loses the control connection must present a stopped/faulted state
and require a new explicit Start after normal HASE recovery. No hidden session
survives as an automatically reusable authorization.

## Initial media profile

The intended negotiation allowlist is:

| Kind | Initial target |
| --- | --- |
| Video codec | VP8 |
| Audio codec | Opus |
| Video size | Nominal 640x480 |
| Video rate | Up to 30 frames per second |
| Audio | Optional; disabled unless requested, supported, and authorized |

Later implementation must confirm the effective browser capabilities and
produce deterministic tests for filtering and rejection. The values are an
interoperability ceiling and negotiation target, not a promise that every
camera exposes that exact native format. Bounded local conversion or a lower
negotiated rate may be used if explicitly implemented and reported. An
unsupported codec or format fails visibly and never expands the allowlist or
changes transport without a new approved compatibility decision.

## WebView2 boundary

Repository-owned HTML, JavaScript, and style assets are local application
content served through an HTTPS virtual-host mapping. The initial boundary:

- denies external top-level navigation and unexpected subresources;
- denies new windows, downloads, arbitrary permission prompts, and remote
  content;
- grants camera or microphone access only to the fixed local origin, only while
  the C# session state permits it, and only for the locally configured source;
- validates every web/native message by version, kind, state, length, and
  schema;
- does not evaluate remote script or accept remote URLs, paths, Windows device
  identifiers, or network destinations; and
- releases WebView2 media ownership on every terminal path.

C# is authoritative for HASE session state. WebView2 events are untrusted input
until validated and mapped to a defined state transition.

## Security, privacy, and diagnostics

Session identifiers are random, opaque, short-lived, non-enumerable, and bound
to the principal, Runtime Host, source ID, source generation, and session
lifetime. The same authenticated principal and applicable permission are
required for negotiation, observation, and stop.

The following are never ordinary diagnostic fields or retained validation
evidence:

- SDP offer or answer;
- ICE candidate, address, port, username fragment, or credential;
- Windows media-device identifier or unredacted friendly name;
- certificate, key, bearer secret, or session token;
- video frame, decoded image, thumbnail, snapshot, waveform, or audio sample;
- WebView2 message body containing negotiation material; and
- arbitrary exception text from browser, driver, codec, or network layers.

Allowed diagnostics are bounded state transitions, sanitized Runtime/source/
session correlation identifiers, requested media kinds, durations, aggregate
byte/frame counters, selected allowlisted codec labels, and enumerated failure
categories. Diagnostics never record media content and must retain the current
HASE local/remote disclosure ceilings.

The version 1 control limits are fixed as follows:

| Limit | Value |
| --- | ---: |
| Source ID or generation | 128 UTF-8 bytes each |
| Session ID | 128 UTF-8 bytes |
| SDP offer or answer | 49,152 UTF-8 bytes |
| ICE candidate | 4,096 UTF-8 bytes |
| ICE candidates | 32 per peer |
| Negotiation messages | 36 per peer |
| Pending delivery messages | 16 |
| Negotiation exchanges | 128 |
| Negotiation idle timeout | 15 seconds |
| Total negotiation lifetime | 60 seconds |
| Renewable session lease | 30 seconds |

Sequence zero, an unspecified message kind, blank required payload, an ICE
completion payload, and any UTF-8 byte overflow are rejected before session or
browser-media state. Stateful count, order, ownership, and timeout enforcement
is required when 55C composes the first session owner; the published constants
cannot be expanded by remote input.

## Existing behavior preserved

Media availability, start, streaming, stop, and failure must not alter:

- endpoint discovery, attachment generations, or authoritative identity;
- Property reads or writes, Commands, Events, and observation;
- at-most-once mutation behavior and uncertain-outcome handling;
- Runtime Host endpoint lifecycle or recovery;
- remote diagnostics authorization, content, ordering, or recovery;
- Client multi-profile selection and existing shortcuts;
- gRPC/mTLS credential, trust, principal, and authorization behavior;
- ESP32 Protocol Version 1 bytes, framing, descriptors, firmware, or transport;
  or
- application startup when media is unconfigured or unavailable.

Media faults are isolated to the media session unless the underlying Runtime
Host process fails. Media recovery never retries or replays an existing HASE
mutation.

## Deployment and network effects

55A has no deployment or network effect. Later stages must separately account
for WebView2 SDK packaging, local assets, Runtime version support, Windows
Firewall behavior, and direct private-network reachability. No stage may open a
firewall rule, change a privacy setting, install a Runtime, publish
configuration, or access a device unless that action is explicit in an
approved increment and preceded by a read-only preflight.

The first physical topology is AEPRAKETE as Runtime Host and LTAEP as Viewing
Client. Naming those machines does not authorize application start, camera or
microphone access, network signaling, capture, deployment, or physical change.

## Automated validation obligations

Before physical validation, automated coverage must prove at least:

- default-deny discovery and every session operation;
- independent video and audio authorization;
- principal, source, and generation binding;
- one-viewer enforcement and no disturbance by a rejected second viewer;
- state-transition validity and idempotent Stop;
- bounded signaling parsing, order, count, size, and timeout behavior;
- no Start replay or media resume after disconnect/reconnect or restart;
- teardown and device release for every terminal path;
- WebView2 origin, navigation, permission, and bridge restrictions;
- codec allowlist and mandatory encrypted transport;
- diagnostic redaction and bounded failure projection; and
- no regression in existing endpoint, Client, security, diagnostics, shortcut,
  and recovery suites.

Physical validation remains a separate 55F authorization after implementation
and automated validation are complete.

## Explicit exclusions

The initial contract excludes recording, snapshots, thumbnails, PTZ, talkback,
Client-originated capture, multiple simultaneous viewers, multiple configured
sources, public-internet operation, STUN/TURN infrastructure, cloud services,
automatic start, automatic resume, background recording, file export, media in
diagnostics, ESP32 media, mobile or browser Clients, and non-Windows Runtime
Hosts.

## 55A rollback boundary

Increment 55A is documentation-only. Rollback removes this contract and its ADR
and restores `docs/ProjectStatus.md` and `docs/Roadmap.md` to the accepted
baseline. There is no source, dependency, configuration, authorization,
credential, deployment, firmware, device, network, or physical state to undo.
