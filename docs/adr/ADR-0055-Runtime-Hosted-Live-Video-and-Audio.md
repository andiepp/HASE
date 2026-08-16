# ADR-0055 — Runtime-Hosted Live Video and Audio

- Status: Accepted; Increment 55E2 encrypted WebView2 peer-boundary source
  prepared and awaiting automated validation
- Date: 2026-08-16

## Context

HASE can discover and control endpoints through Runtime Hosts and can present
authorized live diagnostics in the remote WPF Client. It does not have a
boundary for low-latency camera video or associated microphone audio.

The initial objective is deliberately narrow: one camera explicitly selected
from one or more locally configured sources on a Windows Runtime Host, optional
audio from the microphone locally associated with that camera, and one
view-only remote WPF Client session. The Client explicitly selects a published
logical camera and starts and stops viewing. Existing endpoint, Runtime Host,
Client, security, shortcut, diagnostics, lifecycle, recovery, and ESP32
behavior must remain unchanged.

Continuous media has materially different bandwidth, timing, congestion,
codec, and privacy requirements from HASE Properties, Events, diagnostics, and
request/response RPCs. Treating frames or audio blocks as ordinary HASE data
would couple real-time delivery to the reliable control path and its recovery
semantics. It would also make bounded logging and disclosure substantially
harder.

Diagnostic Export and Offline Analysis remains an accepted backlog item but is
deferred while this objective is active.

## Decision

### Ownership boundary

A Runtime Host media source is a Runtime Host capability. It is not an
Endpoint, Instrument, Property, Command, Event, diagnostic record, or ESP32
Protocol Version 1 capability. No existing endpoint descriptor or southbound
protocol changes for ADR-0055.

The existing HASE gRPC connection protected by mutual TLS is the control plane.
It will own:

- sanitized media-capability discovery;
- exact authorization for capability visibility and each session operation;
- session creation and explicit Client-controlled stop;
- bounded WebRTC signaling exchange;
- sanitized session status and terminal reason; and
- principal-bound cleanup on stop, disconnect, timeout, or process shutdown.

Continuous video and audio use a separate WebRTC media plane. The first
implementation is direct peer-to-peer transport over an operator-managed
private network. WebRTC DTLS-SRTP protects media in transit. STUN, TURN,
public-internet relay, cloud signaling, and cloud media processing are outside
the initial boundary.

The Runtime Host sends video and optional audio; the Client only receives them.
No Client camera, microphone, talkback, remote recording, or remote media
mutation is introduced.

### Initial technology boundary

Microsoft Edge WebView2 embedded in the existing WPF Runtime Host and Client is
the selected initial browser-media boundary.

On the Runtime Host, WebView2 owns `getUserMedia`, browser device opening,
WebRTC sender state, codec negotiation, and encrypted media transport. On the
Client, WebView2 owns the receiving peer, decode, audio playout, and video
rendering. Local web assets are repository-owned and loaded only through an
HTTPS virtual-host mapping. External navigation, new windows, downloads,
permission delegation beyond the selected local origin, and arbitrary web
content are denied.

C# remains authoritative for HASE identity, authenticated principal,
authorization, configuration, session policy, lifecycle, timeouts, shutdown,
and sanitized diagnostics. The web/native bridge is narrow, versioned, size
bounded, and accepts only the defined media-session messages. It does not
accept arbitrary script, URL, Windows device identifier, file path, or network
target from a remote Client.

WebView2 is not currently referenced by the repository. Adding and pinning its
SDK dependency, local assets, and packaging behavior requires a later approved
implementation increment. Runtime presence alone is not treated as repository
integration.

### Source identity and generation

The stable remote identity is:

```text
RuntimeHostId + MediaSourceId + MediaSourceGeneration
```

`MediaSourceId` is a sanitized logical identifier from Runtime Host
configuration, not a Windows device identifier. `MediaSourceGeneration`
changes when the configured source is rebound or its authoritative availability
is recreated. A session request must target the current generation; stale
generation requests fail without opening a device.

The initial implementation supports one or more locally configured camera
sources per Runtime Host and at most one active media session
application-wide. The Client selects only an exact published logical source ID
and generation. Unknown, stale, unavailable, or duplicate logical identities
fail closed without automatic fallback. A second viewer or second session
request is rejected deterministically and does not disturb the active session.

### Lifecycle

The externally meaningful lifecycle is:

```text
Unavailable -> Idle -> Starting -> Negotiating -> Streaming
                                 -> Stopping -> Ended
                                 -> Faulted
```

Availability and authorization do not open a device. Only an authorized,
explicit Client Start action may enter `Starting`. Stop is explicit and
idempotent. Disconnect, lease expiry, negotiation timeout, WebView2 failure,
Runtime Host shutdown, or source loss also tears the session down and releases
camera and microphone ownership.

Runtime Host or Client restart returns media to a stopped state. Existing HASE
profile reconnect and recovery do not replay Start, negotiation, or Stop and do
not resume a previous media session. The operator must explicitly start a new
session against the current source generation.

### Control-plane contract direction

Increment 55B defines the exact protobuf package `hase.runtime.media.v1`, C#
namespace `Hase.Runtime.Media.Grpc.V1`, and unary service
`RuntimeHostMediaControl`. Its operations are limited to:

1. read authorized sanitized media capabilities;
2. start one session for an exact source generation and requested audio mode;
3. exchange bounded, ordered negotiation messages;
4. observe sanitized session status; and
5. stop the caller-owned session.

This is a separately versioned media control service on the existing secured
Runtime Host listener. It does not overload the current endpoint, Property,
Command, Event, observation, or diagnostics services.

The fixed authorization actions are:

```text
media.capability.read
media.video.receive
media.audio.receive
media.session.start
media.session.negotiate
media.session.stop
```

Video receipt never implies audio receipt. Audio is disabled unless the source
supports it, the Client requests it, and the authenticated principal has the
audio grant. Every RPC revalidates the exact principal and required action;
authorization is default-deny.

### Session security

Session identifiers are cryptographically random, opaque, short-lived, and
bound to the authenticated principal, Runtime Host, source identity and
generation. Possession of an identifier is insufficient: negotiation, status,
and stop require the same authenticated principal and applicable grant.

Signaling stays inside authenticated gRPC. Offer, answer, ICE candidate, ICE
credential, local address, Windows device identity, and detailed codec state
are treated as sensitive session material. They are excluded from ordinary
diagnostics, exceptions, UI history, and retained evidence. Logs may contain
only bounded identifiers, state transitions, durations, requested media kinds,
sanitized failure categories, and aggregate counters.

Negotiation inputs have explicit count, byte, character, and lifetime limits.
Unexpected message order, stale generation, unknown session, principal
mismatch, invalid candidate, unsupported codec, or excess input fails closed
and tears down the affected session without changing existing HASE recovery.

No frame, audio sample, decoded image, recording, snapshot, waveform, or media
buffer is persisted by the initial implementation.

### Compatibility contract

The normative initial compatibility contract is
[Runtime-Hosted Media Compatibility Contract](../Runtime-Hosted-Media-Compatibility-Contract.md).
Its key limits are Windows x64, .NET 10 WPF, one explicitly selected locally
configured source per active session, one viewer, direct private-network
WebRTC, DTLS-SRTP, and no relay. The initial negotiation
target is VP8 video and Opus audio, nominally 640x480 at up to 30 frames per
second, subject to implementation-time automated confirmation of both machines'
WebRTC capabilities. Unsupported formats fail visibly; they do not silently
fall back to an unencrypted or control-plane transport.

## Read-only readiness evidence

Increment 55A evaluated prerequisites without opening a camera or microphone.
Both checks required the exact repository baseline
`f6570ad254cd7106af9fa5b926fa40af3343c7d3`, `main`, subject `Close ADR-0054
ESP32 endpoint authoring boundary`, a clean worktree, matching `origin/main`,
and stopped HASE applications.

### AEPRAKETE — Runtime Host

- Windows 10 Pro `10.0.19045`, x64;
- .NET 10 SDK and Windows Desktop Runtime present;
- WebView2 Runtime `151.0.4129.86` present;
- one camera-class device, enabled;
- 24 microphone endpoints, two enabled;
- camera and microphone user consent `Allow`;
- camera and microphone application and desktop policies not configured;
- privacy preflight not denied; and
- private-network interface present.

### LTAEP — Viewing Client

- Windows 11 Pro `10.0.26200`, x64;
- .NET 10 SDK and Windows Desktop Runtime present;
- WebView2 Runtime `151.0.4129.86` present;
- private-network interface present; and
- camera and microphone privacy preflight not denied.

LTAEP also reported local media devices, but Client capture is outside the
initial topology and those devices are not prerequisites for viewing.

The repository has no WebView2 reference at this baseline, as expected. The
readiness checks made no repository, dependency, privacy-policy, device,
network, credential, application, deployment, firmware, serial, or physical
change and did not prove that a particular camera format can stream.

## Alternatives considered

### Media in existing gRPC or HASE capability payloads

Rejected. Reliable request/response and observation paths are not the media
plane. Frame traffic would interfere with bounded control, diagnostics, and
recovery and would create unsafe buffering and replay ambiguity.

### Native Media Foundation/WASAPI with custom RTP/SRTP

Not selected initially. It offers strong Windows integration but requires the
project to own capture interop, codec selection, packetization, congestion,
jitter, encryption, keying, and WPF rendering boundaries. That is too broad for
the first view-only increment.

### MixedReality-WebRTC

Rejected because the project is deprecated and is not an acceptable new
long-term dependency.

### SIPSorcery

Not selected for the initial contract. Its attractive managed signaling
surface does not remove native capture/rendering work, and discovered package
licensing/metadata and VP8 support constraints require more certainty before
adoption.

### FFmpeg, GStreamer, or LibVLC

Not selected initially. They add native redistribution, codec, packaging,
update, and security-patching obligations without providing a smaller HASE
session-control boundary.

### Hosted WebRTC or cloud relay

Rejected for the initial topology. It would introduce an external trust,
credential, availability, privacy, and operating-cost boundary. The first
contract is direct private-network only.

## Risks and required mitigations

- Direct ICE connectivity can fail across Tailscale, VPN, or Windows Firewall
  boundaries. Validation must prove the intended private path; failure stays
  visible and never enables a public relay automatically.
- Evergreen WebView2 updates can change codec or permission behavior. The SDK
  must be pinned and supported Runtime behavior validated, while diagnostics
  report only a sanitized Runtime version and capability outcome.
- Camera formats, drivers, USB bandwidth, CPU load, and hardware acceleration
  vary. Capture constraints need bounded fallback and deterministic failure.
- WebView2 HWND/airspace, process failure, audio autoplay, and WPF lifecycle
  need focused automated and controlled physical validation.
- Camera and microphone access is privacy-sensitive. UI state must distinguish
  available, authorized, starting, streaming, muted, stopped, and faulted; a
  session lease and every terminal path must release devices.
- SDP and ICE are attacker-controlled structured input even inside mTLS. Counts,
  sizes, ordering, lifetime, parser failures, and disclosure must be bounded.

## Implementation stages

1. **55A — complete:** architecture decision, read-only technology discovery,
   compatibility contract, and machine readiness.
2. **55B — complete:** media capability model, authorization actions, protobuf
   control service, fixed input limits, validation, compatibility tests, and
   complete Release validation.
3. **55C — implemented and automatically validated:** Windows Runtime Host
   WebView2 capture and device/session ownership boundary without remote
   viewing; complete Release validation passes 6,113 tests.
4. **55D — revised implementation automatically validated:** WPF Client logical camera selection,
   WebView2 presentation boundary, explicit Start/Stop, audio control, and
   lifecycle UI; complete Release validation passes 6,139 tests.
5. **55E1 — implemented and automatically validated:** authenticated generated
   media-control service, Client gRPC adapter, transport-neutral duplex
   negotiation, acknowledgments, limits, ownership, leases, and 6,158-test
   complete Release validation.
6. **55E2 — source prepared, validation pending:** Runtime Host offerer and
   Client answerer WebView2 peer boundaries with mandatory DTLS-SRTP.
7. **55E3 — planned:** configuration and application composition, packaging,
   failure recovery, security tests, and complete automated validation.
8. **55F — planned:** separately approved controlled physical validation,
   documentation reconciliation, and closure.

Every stage requires a separate proposal and approval. No later stage is
authorized by acceptance of this decision.

## Increment 55B effects, validation, and rollback

Increment 55B adds the separately versioned media protobuf file to the existing
contracts project. It adds the six exact `media.*` permissions, immutable
authorization requirement sets, fixed version 1 negotiation limits, and a
stateless validator for source identity, session identity, message kind,
sequence, payload presence, and UTF-8 byte bounds. Focused tests fix the wire
surface, sanitized fields, VP8/Opus allowlist, independent audio grant,
read-only requirement sets, constants, boundary acceptance, and rejection.

55B does not implement the Runtime Host service adapter or session owner and
does not compose the generated service into Kestrel. It adds no WebView2 SDK,
HTML, JavaScript, XAML, WPF code, capture implementation, device configuration,
authorization-policy grant, deployment tool, or firewall behavior. It cannot
open a camera or microphone, exchange signaling, or transmit media.

Release focused tests for the contracts and adapter projects succeeded on
AEPRAKETE. The complete Release suite passes 6,061 tests with zero failures and
zero skips in 35.0 seconds; the successful build completes in 42.6 seconds with
59 warnings. Exact changed-path and diff checks retain the 17-path scope at the
clean starting baseline
`b745c512f43915a584a840749190a1ca34dc9faa`. No application was started and no
deployment, media-device access, capture, signaling, credential, serial,
firmware, or physical action occurred.

Rollback restores the modified contracts project, permission model, and four
documentation paths and removes the new protobuf, adapter, and test files.
There is no dependency, generated artifact, configuration, credential,
deployment, device, network, firmware, or physical state to undo.

## Increment 55C effects, validation, and rollback

Increment 55C starts from exact commit
`8f5a594053debb53aae120ba72edac415a7a2976` and is committed as
`654ce26560d4e7688984a31bd515a2590ca2448d`. It adds the dependency-light
`Hase.Runtime.Media` project and its test project. The process-local session
owner accepts only one exact configured logical source and generation, keeps
the Windows camera and optional microphone identifiers local, binds one opaque
session to one principal, rejects a second viewer without disturbing the
first, enforces the 55B sequence, count, payload, lease, idle, and lifetime
limits, and releases its injected capture boundary exactly once on explicit
Stop, disconnect, expiry, timeout, source loss, browser failure, shutdown, or
protocol rejection.

The Desktop Host application pins `Microsoft.Web.WebView2` `1.0.4129.50` and
packages repository-owned HTML, JavaScript, and CSS. The non-composed adapter
maps only a fixed local HTTPS virtual host, denies unexpected navigation,
subresources, new windows, downloads, host objects, developer tools, autofill,
password saving, and unrelated permission prompts, and grants camera or
microphone only while C# has an explicit active session. The local capture
script uses exact device constraints and no enumeration or fallback. Its
version 1 web/native envelope carries only fixed lifecycle events and sanitized
failure codes. Remote negotiation is rejected because viewing and end-to-end
WebRTC remain later stages.

Focused validation covers exact source and generation, unavailable and busy
sources, independent audio support, principal ownership, one-session behavior,
ordered and bounded negotiation, timeout and terminal cleanup, local-origin
resource policy, permission revocation, event schema, size limits, and
rejection of expanded or sensitive messages. Those focused suites and the
complete Release suite succeeded on AEPRAKETE. The complete result is 6,113
passed, zero failed, and zero skipped; the successful build reports 56
warnings. Repository application and automated validation do not compose the
adapter into application startup, initialize WebView2, open a device, capture
media, exchange signaling, deploy, change configuration, authorization,
credentials, firewall or privacy state, or perform physical work.

Rollback removes both new media project directories and the Desktop Host media
source/assets, restores the solution, Desktop Host application and test project
files, and restores these four documentation files. No installed Runtime,
application, configuration, device, network, credential, firmware, or physical
state requires rollback.

## Revised Increment 55D effects, validation, and rollback

Increment 55D starts from exact commit
`4f495a27af3ac3e3a4c5aae1a9b3d4a3de981e71` and is committed as
`199356222f8763ed6e6fbb5f481fe46aa70ec679`. It revises the source inventory
without weakening the one-session rule. The process-local owner accepts one or
more unique logical camera configurations and opens only the exact local camera
and optional microphone binding selected by logical ID and generation. The
additive protobuf field `display_name = 7` carries a sanitized operator-defined
label. A deterministic capability mapper publishes the logical target, label,
availability, and fixed VP8/Opus ceilings but never a Windows device ID or
Windows-derived friendly name. Unknown, stale, unavailable, or duplicate
logical identities fail closed without selecting another camera.

The WPF Client adds an explicit camera list, audio opt-in, Start, Stop, and
lifecycle status. With multiple sources it requires a selection; with exactly
one source it may preselect it but never starts automatically. Selection is
locked while a session is active. Changing Runtime Host clears capabilities,
selection, and session state and never replays Start. The abstract media-control
client seam is deliberately not composed to generated gRPC until Increment
55E3, so repository application cannot create a remote media session.

The Client application pins the same WebView2 SDK version as the Runtime Host
and packages receiver-only local assets. The native presentation boundary
denies all camera, microphone, geolocation, notification, and other browser
permissions, external navigation and resources, new windows, downloads, host
objects, developer tools, autofill, and password saving. Its local script has
no device enumeration or `getUserMedia` path. The boundary is not initialized
or composed in this increment; peer transport remains 55E2 and application
composition remains 55E3.

Automated validation covers exact multi-source selection, no fallback,
duplicate logical IDs, deterministic sanitized capability publication,
selection locking and reset, independent audio choice, no automatic Start,
Client WebView2 origin and permission policy, bounded messages, focused
contract/runtime/adapter/Client tests, and the complete Release solution. No
application start, WebView2 initialization, device access, capture, signaling,
deployment, firewall or privacy change, credential change, or physical action
is authorized by application or validation. The focused suites and complete
Release suite succeeded on AEPRAKETE with 6,139 passed, zero failed, and zero
skipped; the successful build reports 45 warnings. A compile-time ambiguity in
an existing target-typed test helper was corrected by naming the
`RuntimeHostMediaSourceConfiguration` type explicitly; production behavior was
unchanged.

Rollback restores all modified documentation, protobuf, project, runtime,
adapter, Client, XAML, and test paths and removes the new mapper, Client media
models, view models, presentation boundary, local assets, and tests. Because
the increment has no deployment, configuration, device, network, credential,
firmware, or physical effect, no external state requires rollback.

## Increment 55E1 effects, validation, and rollback

Increment 55E1 starts from exact reconciled commit
`7b4ffe78920aeaa2e356b5d7a3e84b43ca493dc4`. It completes the previously
abstract transport-neutral control path: the generated five-operation media
service authenticates the existing client principal, evaluates the fixed
permission set for every call, retains exact session ownership, and maps only
sanitized capabilities, lifecycle state, and errors. The Client adapter uses
the existing protected gRPC channel and maps the same logical source and
session contract without logging SDP or ICE.

The process-local owner now separates Host delivery from Client submission.
The Runtime Host alone may publish the initial offer; the Client alone may
submit the answer. Both directions then permit bounded ICE messages using
independent sequences. The exchange acknowledges delivered Host messages,
returns a bounded pending batch, records accepted Client sequence, enforces
count and time ceilings, and renews the lease on successful polls. Invalid
roles, gaps, acknowledgments, duplicates, overflow, timeout, or cross-principal
access fail closed and terminal protocol overflow releases the boundary.

Focused validation covers authorization sets, logical-source redaction,
principal ownership, offerer/answerer roles, delivery replay until
acknowledgment, Client submission, sequence and pending-delivery bounds, lease
renewal, sanitized failures, and Client wire mapping. Focused validation and
the complete Release suite succeeded on AEPRAKETE with 6,158 passed, zero
failed, and zero skipped. The exact 16-path implementation is committed as
`c7d509f43a34948695656614ba5131fb526a4450`.

No 55E1 production registration or application composition is added. Applying
or validating the source does not start an application, initialize WebView2,
enumerate or access a device, capture or render media, create a browser peer
connection, exchange live network signaling, publish configuration, deploy,
or change firewall, privacy, credentials, firmware, or physical state.
Rollback restores the modified media-domain and Client abstractions and tests,
removes the new Host service, authorization gate, wire mapper, Client adapter,
and focused tests, and restores these documentation paths. No external state
requires rollback.

## Increment 55E2 effects, validation, and rollback

Increment 55E2 starts from exact synchronized commit
`c7d509f43a34948695656614ba5131fb526a4450`. It extends the existing
repository-owned WebView2 assets and native adapters without composing them.
The Runtime Host is the only offerer and attaches its already authorized local
camera and optional microphone as send-only transceivers. The Client is the
only answerer, creates no local media, and forces every negotiated transceiver
to receive-only.

Both peers use an empty ICE-server list, zero candidate pooling, maximum
bundling, required RTCP multiplexing, SHA-256 DTLS fingerprints, VP8 video, and
optional Opus audio. They create no data channel and accept no remote URL, ICE
server, device identifier, or network target. Local ICE output is held until
the offer or answer is published, retaining the independent one-based 55E1
sequence contract. Invalid role, order, codec, fingerprint, candidate, or
browser state fails closed with a fixed sanitized category.

The web/native validators accept only the fixed lifecycle and negotiation
envelopes with the existing SDP and ICE byte limits. Focused validation covers
offerer/answerer direction, codec selection, DTLS and RTCP requirements, no
relay configuration, no Client capture, bounded message mapping, sequencing,
sanitized failure, and source-asset policy. Complete Release validation remains
required on AEPRAKETE before commit.

55E2 source application and automated validation do not initialize WebView2,
create a live peer, enumerate or access a camera or microphone, capture or
render media, exchange network signaling, register a gRPC service, compose an
application, deploy, change firewall or privacy state, change credentials,
upload firmware, access serial hardware, or perform physical work. Rollback
restores the two peer assets, native message models, validators and adapters,
focused tests, and these four documentation paths. No external state requires
rollback.

## Increment 55A effects, validation, and rollback

Increment 55A changes documentation only. It adds this ADR and the compatibility
contract and updates Project Status and Roadmap. It adds no source, project,
protobuf, XAML, web asset, package, configuration, authorization, credential,
deployment, or firmware change.

Validation requires exactly those four documentation paths to differ from the
authoritative baseline, no unexpected repository path, and no malformed
whitespace. The complete automated baseline remains the previously established
6,024 .NET tests; 55A does not claim a new test execution.

There are no physical effects. Rollback is the removal of the two added
documents and restoration of the two updated documents to baseline bytes. No
device, dependency, configuration, credential, deployment, or physical state
requires rollback.

## Deferred scope

Recording, snapshots, PTZ, talkback, Client-originated media, public-internet
operation, STUN/TURN or cloud relay, multiple viewers, remotely managed or
automatically selected sources, automatic start or resume, headless
non-WebView2 media, ESP32 media, and physical deployment are outside ADR-0055's
initial objective.
