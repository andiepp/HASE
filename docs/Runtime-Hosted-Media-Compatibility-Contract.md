# Runtime-Hosted Media Compatibility Contract

This document is the normative Increment 55A compatibility boundary for
[ADR-0055](adr/ADR-0055-Runtime-Hosted-Live-Video-and-Audio.md). It constrains
later implementation increments; it does not itself authorize implementation,
device access, media capture, deployment, or physical validation.

## Initial supported topology

```text
one camera explicitly selected from locally configured Runtime Host sources
  + zero or one locally associated Runtime Host microphone
  -> one authenticated HASE Client profile
  -> one view-only WPF Client session
```

- Video direction is Runtime Host send-only and Client receive-only.
- Audio direction, when separately requested and authorized, is Runtime Host
  send-only and Client receive-only.
- Only one active media session is supported application-wide.
- The remote Client selects only a published logical source ID and generation;
  it never supplies or receives a Windows media-device identity.
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
- `DisplayName` is a sanitized operator-defined label. It is not populated
  automatically from a Windows friendly name.
- The exact Windows device identifier and friendly name are local configuration
  data and are not accepted from or disclosed to the Client.
- A Start request for a stale generation is rejected before device opening.
- Missing, disabled, busy, or incompatible devices make the source unavailable
  or fault the requested session without selecting a different device.

No automatic camera or microphone selection or fallback is part of the initial contract.
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

The contract publishes only logical source identity, generation, a sanitized
operator-defined display name, availability, the VP8/Opus allowlist, lifecycle
state, sanitized terminal reason, timestamps, and aggregate counters. Windows
device identifiers, Windows-derived friendly names, network addresses, SDP,
ICE, credentials, tokens, and media content are never capability or status
fields. Negotiation payloads exist only in the dedicated exchange messages and
retain their sensitive classification.

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
is implemented by the 55C process-local session owner; the published constants
cannot be expanded by remote input. The owner is dependency-light, accepts one
exact logical source and generation from its locally configured inventory,
binds the opaque session to one principal, rejects unknown or stale selections
without fallback, rejects a second session without disturbing the first, and
closes the injected capture boundary exactly once on every terminal path.

55C does not compose the owner or generated media service into application
startup. Its WebView2 adapter can open only the exact locally configured device
identities after an explicit owner request, but is not instantiated by the
Runtime Host application in this increment. It rejects negotiation because
remote WebRTC transport remains a later stage.

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

Increment 55C pins the WPF WebView2 SDK at `1.0.4129.50` and packages only
repository-owned local capture assets. The local document fixes a restrictive
content-security policy and uses exact `deviceId` constraints without browser
enumeration or fallback. The native adapter disables developer tools, default
context menus, host objects, downloads, new windows, external navigation,
unexpected subresources, password saving, and autofill. Camera and microphone
permissions remain denied except for the fixed local HTTPS virtual host during
an explicitly active session; microphone permission additionally requires the
session's independent audio request.

The versioned web/native event validator accepts only ready, capture-started,
capture-stopped, and capture-faulted events. Faults use a fixed sanitized code
allowlist; arbitrary driver text, URLs, scripts, paths, device identifiers,
signaling, and media content are rejected. This source boundary adds no
deployment, application start, WebView2 initialization, Runtime installation,
device access, capture, signaling, firewall, privacy, credential, or physical
effect during repository application and automated validation.

The 55C focused session-owner, Desktop Host/WebView2 policy, and retained
media-control adapter tests succeeded on AEPRAKETE. The complete Release suite
passes 6,113 tests with zero failures and zero skips; the successful build
reports 56 warnings. Validation did not initialize WebView2 or access a media
device. The exact 22-path implementation is committed as
`654ce26560d4e7688984a31bd515a2590ca2448d`.

Increment 55D extends the additive version 1 capability message with field 7,
`display_name`. The Runtime Host maps one or more locally configured camera
bindings to unique logical source IDs, generations, sanitized operator labels,
availability, and fixed media ceilings. Publication is deterministically
ordered by display name and logical ID and never includes either local camera
or microphone device identities. Duplicate logical IDs are invalid local
configuration.

The Client presents the published logical cameras for explicit selection and
sends the selected logical ID and generation unchanged on Start. An unknown,
stale, or unavailable selection fails closed; neither the Runtime Host nor the
Client selects a replacement. Only one media session remains active
application-wide, source selection is locked while it is active, and changing
the Runtime Host clears capabilities and session state without replay. A sole
published source may be preselected for convenience, but it is never started
automatically.

The 55D Client WebView2 presentation boundary is receiver-only and remains
uncomposed until encrypted transport is added in 55E. It uses repository-owned
local assets, denies every browser permission including camera and microphone,
performs no device enumeration or `getUserMedia` call, rejects external
navigation and subresources, and accepts only a bounded versioned lifecycle
envelope with sanitized failure codes. Repository application and automated
validation do not initialize WebView2 or access any media device.

The 55D focused media contract, multi-source session-owner, capability
projection, Client selection, and Client WebView2 policy suites succeeded on
AEPRAKETE. The complete Release suite passes 6,139 tests with zero failures and
zero skips; the successful build reports 45 warnings. The single compile-time
validation interruption was corrected by explicitly typing an existing test
helper constructor; it did not change production behavior. Validation did not
start an application, initialize WebView2, enumerate or access a media device,
capture media, exchange signaling, deploy, or change firewall, privacy,
credential, firmware, or physical state.
The exact 27-path implementation is committed as
`199356222f8763ed6e6fbb5f481fe46aa70ec679`.

## Increment 55E1 authenticated duplex control boundary

Increment 55E1 starts from synchronized commit
`7b4ffe78920aeaa2e356b5d7a3e84b43ca493dc4`. It implements, but does not
register, the generated `RuntimeHostMediaControl` service and a Client gRPC
adapter over the existing authenticated channel. Every service operation
requires its fixed media permission set; session operations additionally
require the same authenticated principal that owns the opaque session.

The Runtime Host is the sole offerer. Host-to-Client delivery and
Client-to-Host submission use independent one-based sequence spaces. The Host
may publish one offer followed by ICE candidates/completion; the Client may
submit one answer followed by ICE candidates/completion. An exchange may
acknowledge a previously delivered Host sequence, submit at most one Client
message, and retrieve the bounded pending Host batch. Invalid or regressive
acknowledgments, gaps, duplicates, role reversal, second offer or answer,
overflow, timeout, or ownership mismatch fail closed. An accepted exchange,
including an empty poll, renews the session lease.

SDP and ICE remain sensitive dedicated exchange fields. The adapters do not
log them or project them into capabilities, status, failure details, or
diagnostics. Error projection uses fixed operation statuses or sanitized gRPC
permission/argument details. Local device identities remain outside the wire
contract.

55E1 is transport-neutral repository work. It does not compose the Runtime
Host capture boundary or Client presentation boundary, initialize WebView2,
create `RTCPeerConnection`, enumerate or access a device, capture or render
media, exchange live network signaling, register a service endpoint, change
configuration, deploy, or mutate firewall, privacy, credential, firmware, or
physical state. Focused validation and the complete Release suite succeeded on
AEPRAKETE with 6,158 passed, zero failed, and zero skipped. The exact 16-path
implementation is committed as
`c7d509f43a34948695656614ba5131fb526a4450`.

## Increment 55E2 encrypted WebView2 peer boundary

Increment 55E2 starts from synchronized commit
`c7d509f43a34948695656614ba5131fb526a4450`. The existing repository-owned
Runtime Host capture script becomes the sole WebRTC offerer with send-only
camera and optional microphone transceivers. The Client presentation script
becomes the sole answerer, creates no local media, and forces all negotiated
transceivers to receive-only. Neither peer creates a data channel or accepts a
remote ICE-server or network-target configuration.

Both peers construct `RTCPeerConnection` with an empty ICE-server list,
zero candidate pooling, maximum bundling, and required RTCP multiplexing. They
require SHA-256 DTLS fingerprints and the correct offer/answer setup role in
session descriptions, select VP8 video and optional Opus audio, and fail with
fixed sanitized categories if the required browser capabilities or encrypted
negotiation are unavailable. There is no plaintext or alternate media
fallback.

The version 1 web/native envelopes add only bounded negotiation, connected,
and sanitized fault messages. The Host boundary accepts only Client answer and
ICE input; the Client boundary accepts only Host offer and ICE input. Browser
output uses independent one-based sequences and queues local candidates until
the role-defining offer or answer has been published. SDP and ICE remain
sensitive in-memory values and are not projected into diagnostics or UI.

55E2 does not register or compose either boundary, initialize WebView2, create
a live peer connection, enumerate or access a device, capture or render media,
exchange network signaling, register a gRPC service, publish configuration,
deploy, or change firewall, privacy, credential, firmware, or physical state.
Those application effects remain 55E3 and physical proof remains 55F.

## Increment 55E3 composition contract

Media is absent by default. A Runtime Host exposes media only when its existing
application profile references a valid external version 1 media configuration
and an explicit authorization policy. Source configuration is local custody;
Windows camera and microphone identifiers never enter Client configuration,
capability responses, logs, or retained evidence. Changing a local binding
requires a new opaque source generation.

The media service shares the existing private-network HTTPS/HTTP2 listener,
mTLS authentication middleware, enrolled principal, and exact authorization
policy. It adds no listener or trust path. All six `media.*` permissions remain
independent explicit grants, and no installer or migration grants them
automatically.

WebView2 construction is not device access. Runtime initialization, permission
grant, `getUserMedia`, peer creation, and presentation begin only after an
authorized explicit Start. The Runtime Host marshals every capture-boundary
operation to its WPF dispatcher. The Client uses the selected connected profile
and never captures locally. Its bounded exchange pump does not survive profile
selection change, disconnect, reconnect, peer failure, Stop, or process exit.

Application-only updates preserve the external media configuration exactly.
55E3 does not install WebView2 Runtime, create firewall rules, publish machine
configuration, alter privacy settings, deploy applications, or perform live
media validation.

Focused 55E3 tests and the complete Release suite succeeded on AEPRAKETE. The
complete suite passes 6,202 tests with zero failures and zero skips; the
successful build reports 39 warnings. Automated validation retained the exact
54-path scope without starting either application, initializing WebView2,
accessing a media device, capturing media, creating a live peer, exchanging
live signaling, deploying, changing authorization or credentials, or mutating
physical state.

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

## 55F local binding and enablement custody

Read-only 55F1 discovery confirms that both installed applications require an
application-only update, while existing mTLS credentials, enrollment, and
ordinary authorization remain usable. Application update does not imply media
enablement and must preserve configuration, identity, trust, authorization,
and shortcuts byte-exact.

The 55F2 binding page is a separate repository-owned local-only surface under
the existing `hase-media.local` origin. It is unavailable during normal
Runtime Host startup. It has no network, peer-connection, signaling, or remote
control surface. Exact device enumeration follows an explicit operator action,
temporary tracks are released, and the chosen opaque identifiers are retained
only in a protected local candidate. The production capture page continues to
forbid device enumeration.

Approved 55F4 retains that origin, permission handshake, and protected
candidate format while allowing one through sixteen distinct selected camera
devices. The local page may preview only the current selection and releases
temporary tracks before candidate creation. It never transmits enumeration
results. Deterministic logical IDs and sanitized display names are generated
from the explicit base identity and name; Windows device IDs remain only in
Runtime Host custody. Duplicate logical or video-device identities, zero
sources, more than sixteen sources, malformed members, and oversized input fail
closed. A single-source candidate remains compatible with the existing flow.

Focused automated validation of this 55F4 boundary passes 63 tests. The
complete Release suite passes 6,272 tests with zero failures and zero skips and
reports 61 build warnings. This evidence does not authorize an installed
application update, device enumeration, a new binding candidate, configuration
replacement, or physical multi-camera validation.

A Client preparation request reveals neither an address, certificate
thumbprint, principal, private key, nor policy content in console output. The
Host accepts it only when one requested expected Host identity equals the local
identity and its credential identity matches exactly one existing enrollment.
Media grants are not broad or automatic: video-only configuration adds five
fixed permissions to that principal and audio configuration adds the sixth
audio permission.

Enablement is a stopped-application transaction over exactly the external
media configuration, application-profile reference, and authorization policy.
Every input and pre-state file is hash-bound before mutation. Original profile
and policy bytes plus a bounded transaction manifest remain protected until a
later cleanup approval. Restoration requires unchanged enabled files and
retains its evidence. Repository validation never executes this tooling and
therefore has no deployment, device, authorization, credential, network, or
physical effect.

## Explicit exclusions

The initial contract excludes recording, snapshots, thumbnails, PTZ, talkback,
Client-originated capture, multiple simultaneous viewers, remotely managed or
automatically selected sources, public-internet operation, STUN/TURN
infrastructure, cloud services,
automatic start, automatic resume, background recording, file export, media in
diagnostics, ESP32 media, mobile or browser Clients, and non-Windows Runtime
Hosts.

## 55A rollback boundary

Increment 55A is documentation-only. Rollback removes this contract and its ADR
and restores `docs/ProjectStatus.md` and `docs/Roadmap.md` to the accepted
baseline. There is no source, dependency, configuration, authorization,
credential, deployment, firmware, device, network, or physical state to undo.
