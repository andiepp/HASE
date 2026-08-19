# ADR-0056 — Dynamic Runtime-Host Camera Inventory

- Status: Accepted by Increment 56A; implementation pending
- Date: 2026-08-19
- Starting baseline:
  `d6dbe75bacd3f30e979c8074042db169832bcb5f`
- Starting automated baseline: 6,349 passed, 0 failed, 0 skipped

## Context

ADR-0055 established authenticated Runtime-Hosted live video and optional
audio for one explicitly selected logical camera at a time. It deliberately
used locally configured camera bindings. Production capture opens only the
exact configured device and does not enumerate, select, or fall back to another
camera. The Client obtains a static sanitized capability snapshot and retains a
manual Refresh Cameras action.

That boundary is safe but requires an explicit local binding and installed
configuration replacement whenever the physical camera set changes. It also
cannot update the available Client list while the Runtime Host stays running.
The operator now requires AEPRAKETE to tolerate cameras being plugged in and
disconnected and to reflect the currently available cameras dynamically.

This requirement changes an exclusion of ADR-0055: automatically observed
camera sources were outside its initial objective. The extension therefore
requires a new decision rather than an undocumented bug fix.

## Decision

### Ownership boundary

Dynamic camera inventory is a Windows Runtime Host media capability. It is not
an Endpoint, Instrument, Property, Command, Event, serial attachment, SCPI
device, or ESP32 Protocol Version 1 capability.

The Runtime Host owns:

- local camera-device observation;
- reconciliation of present and absent cameras;
- protected mapping from local device identity to logical HASE source identity;
- per-source availability and generation;
- sanitized capability publication;
- source-loss termination of an active media session; and
- bounded, sanitized inventory diagnostics.

The Client remains view-only. It does not enumerate its own devices, receive a
Runtime Host device identifier, configure a camera, or mutate the Runtime Host
inventory.

### Local observation

A repository-owned, hardened Runtime Host WebView2 inventory boundary will use
browser media-device enumeration at application readiness and after
`mediaDevices.devicechange` notifications. Inventory enumeration does not
open a camera or microphone and must not request or create a media stream.

Device-change notifications are hints rather than authoritative changes. The
Runtime Host debounces them and accepts only a complete successful inventory
snapshot. A failed or incomplete observation does not erase the last accepted
inventory. Exact source availability is revalidated when Start attempts to open
the selected camera.

Production capture continues to use an exact device constraint. It gains no
default-device selection, best-match selection, or fallback.

The accepted maximum remains sixteen published camera sources per Runtime Host.
An excess inventory fails closed with a sanitized bounded diagnostic and does
not publish a partial or ambiguously ordered replacement snapshot.

### Logical identity and protected registry

Raw browser or Windows device identifiers remain local to the Runtime Host.
They are never sent through gRPC, shown in the Client, written to ordinary
diagnostics, or included in repository or validation evidence.

A protected Runtime Host-local registry maps each observed camera identity to:

- a stable sanitized `MediaSourceId`;
- a sanitized display name;
- the current `MediaSourceGeneration`;
- an optional explicit microphone binding; and
- the minimum local metadata needed to recognize the camera again.

The registry is configuration custody, not application custody. Application
updates must preserve it. Creation, migration, replacement, recovery, and
deletion of registry bytes require explicit transactional tooling and retained
recovery evidence.

The two accepted AEPRAKETE camera bindings will seed this registry during a
later approved migration. Their existing logical identities and display names
are preserved. A newly observed camera receives a new logical identity and a
sanitized display name. Duplicate friendly names receive stable distinguishing
suffixes without disclosing the local device identifier.

If the browser supplies no safe label, the Runtime Host assigns a generic
sanitized camera name. A later label improvement may update the display name
but does not change `MediaSourceId`.

### Presence, availability, and generation

The Client camera selector represents cameras in the current accepted available
inventory. A disconnected camera is removed from that list after reconciliation.

A presence epoch has one generation. When a known camera disconnects, its
current generation becomes stale. If it later reconnects, the Runtime Host
preserves its logical source ID but creates a new generation. Requests using
the previous generation fail with `SourceNotCurrent` and never target the
replacement presence epoch.

An unavailable camera that is absent at Runtime Host startup is not published
as available. Its protected registry entry remains locally retained so the
identity can be restored if the same camera returns.

### Active-session behavior

Disconnecting a camera used by an active session:

1. terminates only that media session;
2. releases camera and optional microphone ownership;
3. records terminal reason `SourceLost`;
4. publishes the revised camera inventory; and
5. leaves the Runtime Host, endpoints, instruments, and other Client functions
   operational.

Reconnect never resumes the terminated session. The Client must select a
current source generation and explicitly Start again.

No inventory change automatically selects, starts, resumes, switches, or falls
back to another camera. The one-viewer and one-active-media-session limits
remain unchanged.

### Authenticated capability updates

The media control contract will add an authenticated server-streaming
capability-watch operation. It is additive to
`hase.runtime.media.v1` and uses the existing
`media.capability.read` authorization action.

The stream publishes a complete sanitized snapshot carrying a monotonically
increasing process-local inventory revision. It sends an initial snapshot after
subscription and later snapshots only when the accepted inventory changes.
It contains no browser or Windows device identifier, raw device label,
credential, address, SDP, ICE material, or capture data.

A watch disconnect is read-only and may be re-established by normal Client
connection recovery. Recovery obtains a fresh snapshot but never replays Start,
Stop, negotiation, or another media mutation.

The existing unary capability request remains available as compatibility and
manual recovery behavior.

### Client reconciliation

The WPF Client reconciles a new capability snapshot on its UI dispatcher.

- Sources are ordered by sanitized display name and logical identity.
- A selected idle source is retained only if the same logical ID and generation
  remains present.
- A removed or generation-changed idle selection is cleared.
- With exactly one available source the Client may preselect it, as in ADR-0055,
  but never starts it.
- While a session is active, ordinary selection remains locked.
- A source-loss terminal update ends the session presentation and then applies
  the latest inventory snapshot.
- Manual Refresh Cameras remains a bounded recovery action.

Changing the selected Runtime Host still clears camera capabilities, selection,
audio choice, and session state and never replays Start.

### Audio boundary

ADR-0056 does not introduce dynamic microphone discovery. Existing explicit
camera-to-microphone bindings are preserved during migration. A newly observed
camera is video-only unless a later explicit local configuration transaction
associates a microphone.

Video receipt does not imply audio receipt. The existing
`media.audio.receive` authorization and explicit Client Enable Audio gesture
remain unchanged.

### Security and privacy

The hardened local-origin, navigation, resource, permission, new-window,
download, host-object, developer-tools, autofill, password-saving, message-size,
and schema validation rules remain in force.

Inventory observation does not grant remote configuration authority and does
not broaden camera or microphone capture permission. Only an authorized,
explicit Start against the current logical source and generation may open a
camera.

Ordinary diagnostics may contain:

- inventory revision;
- available-camera count;
- added, removed, or generation-changed counts;
- reconciliation duration; and
- a fixed sanitized outcome or failure category.

They do not contain device identifiers, registry keys, raw labels, device paths,
camera frames, audio samples, signaling, or credentials.

### Compatibility and retained behavior

The following ADR-0055 behavior remains unchanged:

- Windows x64 .NET 10 WPF and pinned WebView2 boundary;
- one Runtime Host media sender and one view-only Client;
- one active application-wide session and one viewer;
- existing mTLS principal and six media authorization actions;
- direct private-network WebRTC with DTLS-SRTP;
- explicit Client selection, Start, Stop, audio request, and audio activation;
- exact generation and no automatic fallback;
- VP8 video and optional Opus audio;
- no recording, snapshot, talkback, Client-originated capture, data channel,
  STUN, TURN, public relay, or cloud media processing; and
- no persistence of camera frames, decoded images, audio samples, or media
  buffers.

The Client and Runtime Host must remain compatible with the existing unary
capability operation while the watch operation is introduced additively.
A Runtime Host that does not support watching continues to require manual
Refresh Cameras.

## Increment plan

### Increment 56A — Architecture acceptance

Documentation only:

- add this ADR;
- update `docs/ProjectStatus.md`; and
- update `docs/Roadmap.md`.

It adds no application, protobuf, generated code, project, WebView2 asset,
configuration, registry, tooling, authorization, credential, deployment,
recovery, camera, microphone, serial, firmware, or physical change.

### Increment 56B — Complete source implementation

The planned repository implementation includes:

- Runtime Host inventory models and reconciliation owner;
- a hardened local WebView2 inventory boundary and bounded bridge messages;
- protected-registry models and transaction-neutral storage seams;
- additive media capability-watch contracts and adapters;
- active-session source-loss integration;
- Client watch lifecycle and UI-list reconciliation;
- sanitized diagnostics; and
- focused success, failure, disconnect, reconnect, stale-generation, security,
  compatibility, and lifecycle tests.

Repository application and automated tests will use controlled fakes and static
asset inspection. They will not initialize production WebView2, enumerate or
open a physical device, publish installed configuration, deploy applications,
or perform physical work.

### Increment 56C — Controlled installed-state migration and validation

After 56B source acceptance, a separately approved transaction will:

- update the AEPRAKETE Runtime Host and LTAEP Client applications;
- migrate the existing two AEPRAKETE camera bindings into protected registry
  custody with retained recovery evidence;
- start the applications under explicit operator control; and
- validate initial inventory, plug-in, idle disconnect, reconnect, active
  disconnect, stale-generation rejection, explicit restart, and unaffected
  endpoint operation.

No automatic retry, fallback, configuration deletion, or recovery cleanup is
authorized.

### Increment 56D — Closure

After source, deployment, and physical evidence are accepted, documentation
will record the final commit, exact automated totals, installed transaction
identity, sanitized physical results, retained recovery custody, and remaining
deferred scope.

## Increment 56A validation

The exact changed path set is:

```text
docs/ProjectStatus.md
docs/Roadmap.md
docs/adr/ADR-0056-Dynamic-Runtime-Host-Camera-Inventory.md
```

Validation requires:

- exact starting commit
  `d6dbe75bacd3f30e979c8074042db169832bcb5f`;
- branch `main` synchronized with `origin/main`;
- a clean starting working tree;
- exactly the three approved documentation paths after application;
- `git diff --check` success;
- internal agreement between this ADR, Project Status, and Roadmap; and
- no application build or test claim.

The retained automated baseline is 6,349 passed, zero failed, and zero skipped.
Increment 56A does not claim a new test execution.

## Physical and deployment effects

None. Increment 56A does not start HASE, initialize WebView2, enumerate or open
a camera or microphone, change installed configuration, create a registry,
change authorization or credentials, deploy software, access serial hardware,
upload firmware, or perform a physical mutation.

## Rollback

Restore `docs/ProjectStatus.md` and `docs/Roadmap.md` to their exact starting
bytes and remove this ADR. No application, configuration, registry,
authorization, credential, deployment, recovery, camera, microphone, network,
serial, firmware, or physical state requires rollback.

## Definition of done

Increment 56A is complete when:

- the exact three documentation paths are applied and reviewed;
- documentation validation succeeds;
- the change is committed and pushed;
- AEPRAKETE, LABC, and LTAEP are synchronized and clean; and
- no application, deployment, device, or physical action has occurred.

ADR-0056 remains open until Increment 56D records accepted source,
installed-state, and physical completion evidence.

## Deferred scope

Dynamic microphone discovery, remote device management, remote camera rename or
deletion, automatic source selection, automatic Start or resume, automatic
fallback or switching, multiple viewers, multiple simultaneous sessions,
recording, snapshots, PTZ, talkback, Client-originated media, STUN/TURN, public
relay, cloud media processing, headless non-WebView2 capture, non-Windows
capture, ESP32 media, and recovery-custody cleanup remain outside ADR-0056.
