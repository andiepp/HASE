# Project Status

## Completed architectural objective — ADR-0052

**ADR-0052 — Python Client Examples and Laboratory Automation — implemented, physically validated, and closed at 569 Python / 5,924 .NET tests**

- Repository-backed examples cover explicit Runtime Host inventory, authoritative Property reads, bounded repeated sampling, bounded live observation, guarded same-value Property writes, and guarded parameterless Command execution.
- Examples consume the installed public `hase` API and standard library only; target selection remains explicit through the external two-target registry.
- Property and Command mutations require explicit confirmation, use the current attachment generation, invoke the mutation boundary once, surface uncertain outcomes, and never retry, replay, reconnect, fail over, or fan out.
- Physical MiniPC validation covered Arduino A0 measurements, live observation including physical button Events, a confirmed same-value built-in LED Property write with authoritative reconciliation, and one confirmed `Led/Toggle` Command with one visible LED toggle.
- The final `hase-laptop-python-minipc` authorization set is exactly `runtime-host.snapshot.read`, `property.authoritative.read`, `observation.subscribe`, `property.write`, and `command.execute`; cached reads and remote diagnostics remain unauthorized.
- The active MiniPC authorization-policy SHA-256 is `b909195329b431c44d77e6e28b24a62413cd2fcec88c2c1ca0b8b0411b16c650`. The retained pre-52G rollback remains exact at `74d1ff1173960f7e39792ce187ef9c9a1a92df5d73094fcea66bf006a3d996b5`.
- Closure validation completed with 13 focused Command-example tests, 569 complete Python tests, 188 focused credential-provisioning tests, and 5,924 complete .NET Release tests.
- Desktop, MiniPC, and Laptop repositories were clean and synchronized at the accepted 52G closure baseline before this documentation-only closure.

### Next

Credential rotation, expiry, replacement, revocation, and recovery belong to a separate architectural objective. ADR-0052 is closed.

---

## Completed architectural objective — ADR-0049

**ADR-0049 — Authorized Remote Runtime Diagnostics — implemented, physically
validated, and closed at 5,726 tests**

- Runtime Host diagnostics are projected through an explicit, live-only,
  bounded stream. The local capture ceiling and configured remote ceiling both
  apply; exact byte snapshots remain bounded to 256 captured bytes.
- Every subscription requires the authenticated principal to hold
  `diagnostics.subscribe`. Missing or absent grants fail closed without
  disclosing policy, identity, credential, certificate, address, or exception
  details.
- Each Client profile owns an independent stream and bounded recovery boundary.
  Diagnostics never retry or replay Property writes or Commands and never
  change uncertain mutation outcomes.
- Projected records retain authoritative Host timestamps, profile/Host scope,
  endpoint and generation scope, correlation metadata, and exact bounded bytes.
  Local Client activity remains distinguishable by its absent remote profile.
- Physical validation confirmed authorized Operational, Protocol, and Bytes
  delivery, fragmented SCPI response preservation, request `0D`, final receive
  chunk `0A`, reconnect without replay or duplicates, and unaffected passive
  KEL-103 recovery.
- Removing only `diagnostics.subscribe` produced sanitized denial while normal
  inventory and authoritative reads continued. Atomic policy and profile
  restoration returned the Host to remote diagnostics disabled with ordinary
  operation intact.
- Validation ended in authoritative CC/OFF state with the external laboratory
  supply output OFF.

### Next

Select the next architectural objective through a separately approved decision
or increment. ADR-0049 is closed.

---

## Completed architectural objective — ADR-0048

**ADR-0048 — SCPI Protocol and Bytes Diagnostics — implemented, physically
validated, and closed at 5,533 tests**

- Increment 48A added optional transport-independent observation inside the
  serialized SCPI exchange without changing the existing constructor or
  production composition.
- Increment 48B mapped Query, Command, byte, completion, classified failure,
  and uncertain-outcome observations into the established runtime diagnostic
  levels.
- Operational capture publishes no SCPI Protocol or Bytes records. Protocol
  capture is correlated and payload-free. Bytes capture additionally owns exact
  snapshots bounded by the existing 256-byte diagnostic limit.
- Increment 48C composed one observer into every production KEL-103 session,
  including replacement sessions. Synchronization, Properties, Commands, and
  passive health continue through the same serialized SCPI gate.
- Increment 48D registered read-only `ScpiText` structured interpretation in
  the Runtime Host for CR-terminated Query and Command requests and
  LF-terminated responses.
- Failure mapping publishes no exception message, serial-port assignment,
  instrument serial identity, Property value, requested value, credential, or
  deployment address at Protocol level.
- Explicit uncertain Command outcome and whether execution may have occurred
  remain visible without retry or replay.
- Physical passive-health and Property-read validation confirmed KEL-103
  endpoint scope, shared correlation, transmitted `0D`, received `0A`, valid
  structured Query/response presentation, unchanged raw bytes, and continued
  `Ready` state.
- The Client boundary is unchanged. Client Diagnostics does not receive or
  reconstruct Runtime Host southbound byte snapshots.
- Validation ended in authoritative CC/OFF state with the external laboratory
  supply output OFF.

### Next

Select the next architectural objective through a separately approved decision
or increment. ADR-0048 is closed.

---

## Completed architectural objective — ADR-0047

**ADR-0047 — Passive SCPI Instrument Health Supervision — implemented,
physically validated, and closed at 5,497 tests**

- Each supervised KEL-103 attachment owns exactly one passive monitor.
- The monitor waits five seconds before the first probe and after every
  completed probe; it performs no catch-up and accumulates no probes.
- Probing occurs only while the endpoint is Ready.
- The fixed health operation sends exactly one characterized read-only
  `*IDN?`, requires valid KEL-103 identity, and changes no Property cache.
- The published connection-slot gate and serialized SCPI-session gate prevent
  overlap with Property reads, writes, Commands, probes, or replacement.
- Probe failure projects fixed sanitized Faulted state. Existing supervision
  owns replacement and complete authoritative read-only synchronization.
- No mode, setpoint, input, or SHORT mutation is retried or replayed.
- Orderly shutdown stops the monitor before recovery supervision and
  attachment disposal; lifecycle cancellation is not reported as a
  communication failure.
- Physical USB removal without an operator operation caused Host and Client to
  leave Ready. Reconnection returned both to Ready with authoritative CC/OFF
  state, other endpoints operational, and sanitized diagnostics.
- Host, Client, northbound, definition, profile, and deployment contracts
  remain unchanged.

### Next

Select the next architectural objective through a separately approved decision
or increment. ADR-0047 is closed.

---

## Completed architectural objective — ADR-0046

**ADR-0046 — Controlled KEL-103 Operating State and Setpoints — implemented,
physically validated, and closed at 5,479 tests**

- The objective adds authoritative mode, input state, and voltage, current,
  resistance, and power targets above the completed ADR-0045 attachment.
- Controlled modes are CC, CV, CR, CW, and SHORT.
- Setpoints become read/write Properties; mode and input behavior use explicit
  Commands.
- Mode and setpoint changes require authoritative input OFF.
- Generic activation rejects SHORT; short-circuit activation has a distinct
  Command requiring explicit Boolean confirmation.
- Every state change is sent once, authoritatively read back, never retried, and
  never replayed during recovery.
- Unsupported input-state query behavior blocks input-control publication.
- ADR-0045 version 2 remains immutable; definition migration is explicit and
  offline.
- Physical read-only characterization established exact mode responses `CC`,
  `CV`, `CR`, `CW`, and case-sensitive `SHORt`.
- Exact input-state responses are `OFF` and `ON`; the ON response was observed
  only during attended manual activation with the external supply output off.
- Voltage, current, resistance, and power targets use invariant numeric text
  with exact suffixes `V`, `A`, `OHM`, and `W`.
- All characterization queries were identity-gated and read-only. Final state
  was restored to CC and OFF with original setpoints unchanged, and independent
  port reopening succeeded.
- Physical limit characterization established the separate `LOW?` and `UPP?`
  paths and exact voltage, current, resistance, and power bounds.
- The rejected `:VOLTage? MIN` candidate timed out without retry or mutation;
  `MIN` and `MAX` remain excluded setter tokens rather than limit-query
  parameters.
- Physical mode-selection characterization established exact commands and
  readbacks for CC, CV, CR, CW, and case-sensitive `SHORt`.
- `:FUNCtion VOLT` and all-uppercase `:FUNCtion SHORT` were rejected after one
  transmission each and were not retried.
- Every successful mode probe kept input OFF, preserved all four setpoints,
  transmitted the destination once, restored CC once, and closed in CC/OFF
  state with the external supply output off.
- SHORT selection did not activate the input and remains separate from the
  later explicitly confirmed short-circuit activation gate.
- Physical setpoint-write characterization established exact invariant setter
  forms for voltage, current, resistance, and power with their exact units.
- Voltage setters select CV, current setters select CC, resistance setters
  select CR, and power setters select CW; setpoint writes are not mode-neutral.
- Same-value and bounded changed-value probes kept input OFF, preserved
  unrelated targets, restored the original selected target once, and restored
  CC once where required.
- Every successful setter probe closed in CC/OFF state with all original
  targets restored while the external supply output remained off.
- No value or bound was disclosed, and no mutation was retried or replayed.
- Definition versions 1 through 4 remain immutable. Definition version 5 adds
  only the three controlled input Commands to the version-4 inventory.
- Definition version 3 adds read-only mode, input state, and all four target
  Properties to the existing identity and measurement inventory.
- Definition version 4 makes only the four targets read/write and adds five
  parameterless mode-selection Commands for CC, CV, CR, CW, and SHORT.
- Definition version 5 adds parameterless `Input.Activate` and
  `Input.Deactivate`, plus `ShortCircuit.Activate` with one required Boolean
  confirmation argument whose normalized value must be `true`.
- SHORT mode selection does not activate the input. Generic activation rejects
  SHORT, while confirmed SHORT activation requires authoritative SHORT/OFF
  state immediately before its single transmission.
- Version-3 state reads and version-4 setpoint writes and mode Commands now
  cross the serialized runtime, hosting, attachment, Host, and Client paths.
- The installed profile migrated explicitly and offline from version 2 to
  version 4 with atomic replacement, retained backup, and preserved endpoint
  and serial-profile custody.
- Setpoint and mode mutations enforce authoritative input OFF, transmit once,
  read back authoritatively, expose uncertainty, and are never retried or
  replayed through recovery.
- The Runtime Host presents an ordered mode selector. The Client uses five
  direct descriptor-driven CC, CV, CR, CW, and SHORT buttons.
- The Client preserves a pressed mode button only through that single click so
  same-generation observation refresh cannot swallow it; host, connection, and
  generation changes are never deferred.
- Physical Client validation confirmed all five selections, one successful
  Command per accepted press, SHORT remaining OFF, and final CC/OFF restoration
  while the external supply output remained off.
- Automated version-4 recovery coverage confirms uncertain setpoint and mode
  mutations are transmitted once, fault the session, and are never replayed by
  the replacement session.
- Recovery retains the published endpoint and operation ports, synchronizes all
  eleven Properties through identity and read-only queries, and replaces cached
  values only with authoritative recovered reads.
- Physical USB-disconnect validation confirmed that offline front-panel mode
  and target changes are adopted on recovery without replay, with unchanged
  endpoint identity and attachment generation.
- Increment 46H established the former operation-detected idle-loss limitation;
  ADR-0047 subsequently adds passive idle detection without changing ADR-0046
  mutation or recovery semantics.
- All post-recovery reads and explicit mode Commands succeeded, diagnostics
  remained scoped and sanitized, and final state was CC/OFF with the external
  supply output off.
- The installed profile migrated explicitly and offline from version 4 to
  version 5 with strict preflight validation, atomic replacement, retained
  version-4 backup, and preserved endpoint and serial-profile custody.
- Input activation and deactivation now cross the runtime, hosting, attachment,
  Runtime Host, and Client paths. The Host and Client expose dedicated Activate
  input and Deactivate input controls.
- The Client exposes confirmed SHORT activation separately from mode selection.
  Its strict two-state confirmation is retained only across connected
  same-host, same-generation observation refreshes and is cleared after
  execution or any host, connection, or attachment-generation boundary.
- Physical Host and Client validation confirmed ordinary activation,
  deactivation, and separately confirmed SHORT activation with authoritative
  readback and matching instrument state. Each accepted mutation produced one
  Command execution; no retry or recovery replay occurred.
- Validation kept the external laboratory supply output OFF and ended in
  authoritative CC/OFF state. It validates the complete control path without
  claiming energized electrical-load performance.

### Next

Select the next architectural objective through a separately approved decision
or increment. ADR-0046 is closed.

---

## Completed architectural objective — ADR-0045

**ADR-0045 — Runtime-Hosted SCPI Instrument Publication — implemented,
physically validated, and closed at 4,772 tests**

- The production Desktop Runtime Host explicitly attaches and publishes the
  physical KEL-103 through the authoritative inventory.
- One normalized electronic-load instrument exposes product identity, firmware,
  voltage, current, and power as five read-only Properties.
- No writable Property, Command, raw SCPI console, or SCPI-specific northbound
  contract is exposed.
- Publication follows identity verification and complete synchronization.
- USB reconnect and complete instrument power-cycle recovery replace the
  connection, reverify identity, resynchronize fully, and retain the published
  attachment generation.
- Host, Client, gRPC, diagnostics, and multi-host contracts remain unchanged and
  correctly scoped.
- Operational diagnostics are useful and sanitized. ADR-0048 subsequently adds
  level-controlled SCPI Protocol and Bytes diagnostics without changing this
  publication boundary.
- Physical validation covered simultaneous Desktop Arduino, ESP32, and KEL-103
  operation plus a MiniPC Arduino through two authenticated Client sessions.

## Completed architectural objective — ADR-0044

**ADR-0044 — SCPI Instrument Adapter Boundary — accepted; SCPI session and
KEL-103 characterization migration complete through 44B5C**

- Increment 44A1 establishes the architecture and the KORAD KEL-103
  programmable DC electronic load as the first physical validation target.
- Increment 44A2 adds a bounded Protocol Explorer characterization utility that
  sends exactly one fixed read-only `*IDN?` query with an explicitly selected
  terminator.
- 31 automated tests cover request bytes, serial settings, partial reads,
  terminators, identity recognition and redaction, timeouts, cancellation,
  disposal, and argument parsing.
- Physical characterization verified 115200 baud, 8 data bits, no parity, one
  stop bit, no flow control, ASCII command and response text, CR command
  termination, LF response termination, and no command echo.
- One observed run returned 33 bytes, delivered its first byte after 4.3 ms, and
  completed after 213.6 ms through the configured 200 ms post-byte idle bound.
  These timings are observations, not production guarantees.
- The first physical attempt exposed that Windows
  `SerialPort.BaseStream.ReadAsync` may ignore cancellation while awaiting the
  first byte. The corrected utility races the read against an independent timer
  and disposes the owned port when the timer wins.
- The corrected physical run completed normally, verified product and firmware,
  redacted the instrument serial identity, sent no state-changing command, and
  released the port for immediate reuse by another application.
- Machine-specific serial-port targets and returned instrument serial identities
  remain external deployment data.
- Existing HASE endpoint, instrument, Property, Command,
  attachment-generation, northbound, and multi-host boundaries remain
  unchanged.
- The dependency-free SCPI core now provides deterministic ASCII framing,
  serialized queries and commands, uncertain command outcomes, bounded active
  exchanges, desynchronization faulting, and deterministic shared disposal.
- Protocol Explorer adapts the validated KEL-103 serial profile to the generic
  byte-stream boundary without moving device-specific behavior into `Hase.Scpi`.
- The migrated physical run verified LF framing, no echo, product and firmware,
  redaction, normal exit, and immediate independent port reopening.
- Current verified baseline: 4,515 automated tests passing.

### ADR-0044 closure

Runtime publication continues separately under ADR-0045.

### Previous completed architectural objective

ADR-0043 — Repeatable Runtime-Host Deployment, Enrollment, and Multi-Host Client
Topology — complete through Increments 43A to 43H at 4,405 passing tests.

### ADR-0043 completion detail

**ADR-0043 — Repeatable Runtime-Host Deployment, Enrollment, and Multi-Host
Client Topology — complete**

- ADR-0043 accepted on 2026-08-01.
- Increments 43A through 43H are complete.
- Authoritative starting commit:
  `c79d956de4603412c431425a94a7dac17ffae98d`.
- Starting baseline: 4,017 automated tests passing.
- Existing private-network deployment and client documents remain the
  lower-level security configuration.
- External application profiles compose one Runtime Host installation
  and an ordered client registry of expected Runtime Hosts.
- Runtime Host identity is installation-safe according to ADR-0024.
- Certificate authentication and pinning remain mandatory; the client verifies
  the authoritative `RuntimeHostId` from the initial snapshot.
- The multi-host coordinator owns several independent existing single-host
  session controllers.
- Endpoint operations in a multi-host client are qualified by Runtime Host,
  endpoint, and attachment-generation identity.
- Tailscale remains reachability only and does not become HASE identity or
  authorization.
- Client registry migration and safe offline add, enable, disable, remove, and
  backup recovery are physically validated.
- Independently installed Desktop and MiniPC Runtime Hosts operated
  simultaneously while one laptop Client maintained both authenticated
  sessions.
- Inventories, Property operations, Commands, Events, diagnostics, and
  independent disconnect/reconnect behavior were physically validated without
  cross-host attribution or lifecycle interference.
- Current verified baseline: 4,405 automated tests passing.

### Recent completed architectural objectives

- ADR-0038 — Descriptor-Driven Command Argument Editing — complete.
- ADR-0039 — Descriptor-Driven Event Presentation — complete at 3,762 tests.
- ADR-0040 — Structured Runtime Diagnostics and Tracing — complete at 3,913
  tests after physical ESP32 and Arduino Uno validation.
- ADR-0041 — Desktop Diagnostics Window and Presentation Pause — complete at
  3,981 tests, including structured Native and Compact byte interpretation.
- ADR-0042 — Laptop Client Diagnostics Window and Presentation Pause — complete
  at 4,017 tests; all thirty-seven combined physical checks passed without
  deviation.

### Next

Proceed only after explicit approval with Increment 44A2 — Read-Only KEL-103
Serial Characterization.

---

## Project

**HASE - Hardware Access System Environment**

HASE is an open, modular framework for describing, discovering, communicating
with, and controlling hardware instruments independently of transport
technology.

---

# Overall Status

**Current Phase:** Phase 7 - Northbound Runtime-Host API

The core architecture, runtime model, simulation framework, Protocol Version 1,
Compact Serial Protocol Version 1, runtime integration, Protocol Explorer,
production TCP and USB serial transports, duplex protocol infrastructure,
endpoint synchronization, automatic connection recovery, active protocol health
probing, runtime event routing, transport diagnostics, physical property access,
physical command execution, physical event notification, IPv4 network endpoint
discovery, explicit runtime-host-owned endpoint attachment, the authoritative
runtime-host attachment inventory, compact runtime property synchronization,
compact serial connection supervision, Windows USB serial discovery, compact
serial endpoint attachment, compact serial unsolicited event notification,
normalized northbound Property operations, normalized northbound Command
execution, normalized northbound live observation, versioned loopback gRPC
remote API mapping, northbound authorization, certificate authentication, and
mutual-TLS Kestrel hosting, authenticated physical northbound Property
validation, authenticated physical northbound Command validation, and the
ADR-0032 controlled private-network runtime-host deployment profile are
implemented. Phase 6 is complete at the C-025 baseline.

ADR-0023 defines the Phase 7 northbound runtime-host API boundary. Phase 7 begins
with transport-independent application services that expose the authoritative
runtime-host inventory and normalized Properties, Commands, Events, connection
status, and live observation without exposing or transferring ownership of
physical endpoint lifecycles.

The Phase 7 snapshot, identity, inventory-query, normalized Property, normalized
Command, and live-observation foundations are implemented. They provide
stable runtime-host identity, API contract versioning, immutable endpoint
attachment snapshots, opaque attachment generations, authoritative inventory
list and lookup projection, identity resolution precedence, atomic
cross-process file persistence, file-backed snapshot composition, cached
Property queries, authoritative Property reads, endpoint-confirmed Property
writes, generation-scoped Command execution, shared generation authority,
normalized native/compact operation results, and snapshot-first bounded
subscriptions for lifecycle, connection, Property, and Event observations.
Phase 7.7 maps that boundary through versioned protobuf and ASP.NET Core gRPC
without transferring endpoint lifecycle ownership. Unary snapshot, Property,
and Command operations and server-streaming observation are integrated over
loopback HTTP/2. ADR-0031 defines the security boundary. C-029 through C-034
implement authorization, certificate authentication, and mutual-TLS Kestrel
integration, including physically verified authenticated authoritative Property and Command
RPCs. ADR-0032 adds a controlled non-loopback validation profile with external
configuration, operating-system credential custody, exact server-certificate
pinning, explicit client enrollment, and a desktop-owned two-endpoint physical
inventory. Unrestricted production or Internet exposure remains prohibited.

C-016 and C-017 are validated through the physical ESP32/BME280 endpoint.
C-018 through C-025 are validated through the physical Arduino Uno endpoint.
C-026 validates the same public northbound Property service through both
physical endpoint families.
C-027 validates the same public northbound Command service through both
physical endpoint families.
C-028 validates the same public northbound live-observation service through
both physical endpoint families.
C-032 validates an authenticated authoritative Property RPC through the
mutual-TLS gRPC host against the physical ESP32/BME280 endpoint.
C-033 validates authenticated Command execution and authoritative state
confirmation through the mutual-TLS gRPC host against the physical Arduino Uno.
C-034 validates authenticated server-streaming observation through the
mutual-TLS gRPC host against the physical ESP32/BME280 and GPIO17 endpoint.
ADR-0032 validates authenticated snapshot, Property, Command, and observation
access from a separate laptop to one desktop runtime host owning both the
physical ESP32 and Arduino endpoints.

ADR-0033 provides the production WPF Laptop Client, API documentation, and
tutorial. ADR-0034 provides the production Windows Desktop Runtime Host
application and persistent live inventory. ADR-0035 completes the interactive
operator console with Boolean Property writes, parameterless Commands,
authoritative post-Command Property reconciliation, bounded operator activity,
persistent Event descriptors, and bounded live Event occurrences. ADR-0036
adds immutable opaque ByteArray values, typed Command arguments, compatible
Protocol Version 1 extensions, complete gRPC/client mapping, and an opt-in
in-process ByteArray validation endpoint exercised from the remote WPF client.
ADR-0037 adds shared descriptor-driven Property input semantics, typed editors
for both WPF applications, a writable four-type validation simulation, and
validated local and remote authoritative writes.

The current verified baseline is:

```text
4,772 automated tests passing
.NET solution builds
ESP32 firmware builds
Arduino Uno firmware builds
Physical ESP32 endpoint verified
Physical Arduino Uno endpoint verified
IPv4 mDNS/DNS-SD discovery verified
Windows USB serial discovery verified
Compact serial endpoint attachment verified
Compact serial event notification verified
Arduino Uno USB-unplug/replug recovery verified
Arduino Uno hardware-reset recovery verified
Physical native and compact northbound Property access verified
Physical native and compact northbound Command execution verified
Physical native and compact northbound live observation verified
IPv4 loopback HTTP/2 gRPC integration verified
IPv6 loopback HTTP/2 gRPC integration verified where supported
Mutual-TLS HTTP/2 gRPC hosting verified
Missing and untrusted client-certificate rejection verified
Authenticated principal projection verified
Authenticated physical ESP32 Property RPC verified
Rejected Property credentials never reach the Property service
Authenticated physical Arduino Command RPC verified
Rejected Command credentials never reach the Command service
Authenticated physical ESP32 observation stream verified
Rejected observation credentials never open a subscription
Physical AttachmentPublished, PropertyValueChanged, EventOccurred, and AttachmentEnded verified through mutual TLS
Controlled private-network mutual-TLS deployment verified
Desktop-owned two-endpoint snapshot verified from a separate laptop
Private-network authoritative Property access verified for both endpoint families
Private-network Arduino Command confirmation and state restoration verified
Private-network Property and physical Event observation verified
Orderly client, stream, host, and physical endpoint shutdown verified
Desktop Runtime Host operator console verified
Physical ESP32 and Arduino Boolean Property writes verified
Physical ESP32 and Arduino parameterless Commands verified
Post-Command authoritative Property reconciliation verified
Bounded operator activity projection verified
Physical ESP32 and Arduino Event descriptors verified
Live Event occurrence source attribution verified in both endpoint orders
Desktop Runtime Host window and process shutdown verified
Physical KEL-103 identity, voltage, current, and power access verified
KEL-103 Runtime Host publication and authoritative Client reads verified
KEL-103 USB reconnect and complete instrument power-cycle recovery verified
Native, Compact, and SCPI endpoint coexistence verified
Four-endpoint simultaneous Desktop and MiniPC topology verified
Sanitized KEL-103 Host and Client Operational diagnostics verified
KEL-103 orderly shutdown and independent serial-port reopening verified
```

Protocol Version 1 is feature complete for the current endpoint contract.

---

# Completed Phases

## Phase 1 - Foundation

Completed:

- core domain model;
- endpoint and instrument identity model;
- descriptor model;
- engineering quantity and unit model;
- runtime context;
- runtime endpoint and instrument model;
- runtime property cache;
- endpoint connection status;
- architecture documentation;
- initial architecture decision records.

## Phase 2 - Simulation

Completed:

- `Hase.Simulation`;
- simulation host and simulation steps;
- environment simulation and environment state;
- value-generator hierarchy;
- periodic waveform generators;
- simulated environment sensor;
- simulation/runtime integration;
- simulation tests.

## Phase 3 - Protocol Foundation

Completed:

- Protocol Version 1 architecture;
- protocol message roles and types;
- correlation identifiers;
- protocol envelopes;
- binary envelope and payload serialization;
- descriptor serialization;
- Variant serialization;
- property-value serialization;
- protocol paths;
- String, Numeric, and Boolean data-descriptor serialization.

## Phase 4 - Protocol Version 1

Completed:

- `DiscoverRequest` and `DiscoverResponse`;
- `ReadEndpointDescriptorRequest` and `ReadEndpointDescriptorResponse`;
- `ReadPropertyRequest` and `ReadPropertyResponse`;
- `WritePropertyRequest` and `WritePropertyResponse`;
- `ExecuteCommandRequest` and `ExecuteCommandResponse`;
- `EventNotification`.

Protocol Version 1 supports Properties, Commands, and Events. It supports full
embedded descriptors and compact descriptor references. Network-discovery
metadata is not part of the Protocol Version 1 wire contract.

## Phase 5 - Runtime Integration

Completed:

- runtime protocol dispatcher;
- property, command, and event routing;
- runtime protocol client;
- loopback protocol integration;
- Protocol Explorer;
- logical, message, and byte tracing;
- end-to-end runtime capability tests.

## Phase 6 - Transport Infrastructure and Physical Endpoint Integration

Phase 6 is complete at the C-025 baseline.

Completed:

- transport abstraction and loopback transport;
- production framed TCP transport;
- transport connection lifecycle and health tracking;
- transport exchange diagnostics;
- duplex transport connections and protocol sessions;
- correlated response and unsolicited notification routing;
- coordinator-owned duplex session lifecycle;
- endpoint synchronization;
- automatic initial connection retry and transport replacement;
- complete resynchronization after reconnect;
- cached-property preservation while disconnected;
- active protocol health probing and silent-loss detection;
- runtime event-router migration across replacement sessions;
- physical ESP32 endpoint integration;
- physical BME280 environment sensor and GPIO controller;
- physical property reads and writes;
- physical command execution;
- physical GPIO17 event notification;
- physical reconnect and event-recovery validation;
- IPv4 mDNS/DNS-SD endpoint discovery;
- Protocol Version 1 candidate verification;
- authoritative endpoint-ID deduplication;
- Protocol Explorer network-discovery scenario;
- explicit endpoint connection and descriptor-source contracts;
- native Protocol Version 1 bootstrap and authoritative identity validation;
- staged runtime endpoint creation and readiness-gated publication;
- runtime-host-owned attachment sessions and orderly shutdown;
- manual and discovery-derived network definitions through one attachment path;
- automated framed-TCP attachment lifecycle integration;
- physical C-016 attachment and shutdown validation;
- authoritative runtime-host attachment inventory;
- immutable attachment inventory entries;
- attach, find, snapshot list, detach, and asynchronous disposal operations;
- duplicate-identity rejection without automatic replacement;
- deterministic attachment, detachment, and disposal coordination;
- runtime attachment-host composition;
- native framed-TCP host composition;
- automated host-inventory framed-TCP integration;
- physical C-017 inventory attachment and detachment validation;
- production USB serial byte transport for Arduino Uno-class endpoints;
- Compact Serial Protocol Version 1 framing, correlation, and CRC validation;
- versioned host-side compact endpoint descriptor resolution;
- physical C-018 compact bootstrap and descriptor-resolution validation;
- compact command execution and physical C-019 LED-toggle validation;
- descriptor-side compact property mappings and Boolean value decoding;
- compact property reads and physical C-020 LED-state validation;
- compact runtime property synchronization with cache-preservation semantics;
- compact serial connection ownership and coordinated connection replacement;
- recurring compact endpoint health probing with explicit interval and timeout;
- automatic compact serial recovery using immediate, 1-second, 2-second,
  5-second, and bounded 10-second retry delays;
- cache preservation during compact serial faults and property refresh after
  recovery;
- clean cancellation-aware compact supervision shutdown;
- physical C-021 USB-disconnection detection, retry, reconnection,
  resynchronization, and shutdown validation;
- compact property-write request and response wire contracts;
- descriptor-selected compact Boolean encoding and writable-property validation;
- coordinator-owned compact property writing serialized against replacement and
  shutdown;
- endpoint-confirmed read-back with runtime-cache update only after successful
  confirmation;
- Arduino Uno writable `Led.State` firmware support;
- physical C-022 `Off -> On -> Off` property-writing and cache-confirmation
  validation;
- Windows USB serial candidate enumeration through `Win32_PnPEntity`;
- platform-neutral USB serial candidate and metadata-filter contracts;
- sequential compact candidate verification with isolated expected outcomes;
- authoritative compact endpoint identity from
  `CompactBootstrapResponse.EndpointId`;
- candidate deduplication by normalized connection target and verified-inventory
  deduplication by authoritative `EndpointId`;
- production Windows USB serial discovery composition with temporary connection
  ownership and no runtime attachment;
- Protocol Explorer C-023 automatic Arduino Uno discovery and authoritative
  bootstrap validation;
- configured and discovery-derived compact serial definitions converging on one
  attachment service;
- host-repository compact endpoint definitions combining exact descriptor
  references, complete descriptors, property mappings, and event mappings;
- temporary authoritative attachment bootstrap followed by an independent
  operational compact connection;
- strict operational identity, descriptor, and definition revalidation;
- readiness-gated publication after initial readable-property synchronization;
- shared native and compact attachment lifecycle ownership and failure cleanup;
- compact runtime-host composition and authoritative attachment inventory
  integration;
- explicit compact endpoint detachment with orderly supervision and connection
  shutdown;
- Protocol Explorer C-024 explicit selection, attachment, synchronization,
  inventory, and detachment validation;
- Compact Serial Protocol unsolicited `EventNotification` message type;
- correlation identifier zero reserved for unsolicited compact notifications;
- one compact connection reader for correlated responses and unsolicited events;
- descriptor-side compact event-ID mapping to `InstrumentId` and `EventPath`;
- current-connection-authoritative compact event publication;
- stale/replaced connection event suppression;
- compact event routing into the existing `RuntimeEvent` model;
- runtime observer continuity across compact physical connection replacement;
- no compact offline event queue and no replay after reconnect;
- deterministic compact event shutdown behavior;
- Arduino Uno D7 active-low `INPUT_PULLUP` event publisher with 50 ms debounce;
- Protocol Explorer C-025 physical compact event validation;
- bounded compact connection/bootstrap attempts during supervision;
- physical C-025 recovery after Arduino hardware reset while USB remains
  connected;
- physical C-025 recovery after USB unplug/replug.

---

# Phase 7 - Northbound Runtime-Host API

Phase 7 is active.

Architecture: ADR-0023 - Northbound Runtime-Host API Boundary.

The approved northbound architecture:

- places the northbound runtime-host API above the authoritative attachment
  inventory and runtime model;
- keeps the runtime host as the sole owner of every physical endpoint
  connection, protocol or adapter session, synchronization service, recovery
  supervisor, notification route, and attachment lifetime;
- exposes immutable host, endpoint, instrument, Property, Command, Event, and
  connection-state representations;
- normalizes native Protocol Version 1 and Compact Serial Protocol endpoint
  operations behind transport-independent application services;
- distinguishes cached Property queries from explicit authoritative endpoint
  reads;
- preserves endpoint-confirmed Property-write semantics;
- binds active operations to both authoritative `EndpointId` and an opaque
  attachment generation;
- prevents operations from an ended attachment from crossing into a later
  attachment with the same endpoint identity;
- supports multiple local or remote applications without sharing the physical
  endpoint connection;
- preserves transient Event semantics with no offline queue and no replay;
- separates the initial operational API from future remote lifecycle
  administration;
- keeps Tailscale reachability and runtime-host discovery separate from the API
  contract;
- defers remote wire-technology, authentication, authorization, encryption, and
  audit decisions until after the transport-independent service boundary is
  implemented and validated.

Implemented Phase 7 foundation:

- dedicated `Hase.Runtime.Northbound` project;
- `RuntimeHostApiVersion`;
- stable authoritative `RuntimeHostId`;
- immutable runtime-host and published endpoint snapshots;
- authoritative inventory list and lookup projection;
- opaque per-attachment generations that change across reattachment;
- runtime-host snapshot capture from the host-owned attachment inventory;
- ADR-0024 stable runtime-host identity semantics;
- ADR-0025 explicit, persisted, and generated-and-persisted identity
  resolution;
- canonical GUID-based runtime-host identity generation;
- ADR-0026 strict version-1 JSON file persistence;
- bounded strict UTF-8 document validation;
- atomic non-overwriting first-run identity publication;
- concurrent first-run convergence on one authoritative identity;
- file-backed runtime-host snapshot composition;
- shared attachment-generation authority for snapshots and active operations;
- immutable generation-scoped Property targets and cached snapshots;
- normalized cached-query and authoritative-operation statuses;
- attachment-bound Property operation ports;
- native Protocol Version 1 and Compact Serial Protocol Property adapters;
- logical compact Property reverse lookup without exposing compact identifiers;
- descriptor-based requested-value validation;
- normalized cached Property queries, authoritative reads, and
  endpoint-confirmed writes;
- public `IRuntimeHostPropertyService`;
- snapshot composition exposing the Property service over the exact same
  attachment projection used by inventory snapshots;
- automated native/compact contract integration;
- physical C-026 native cached and authoritative temperature reads;
- physical C-026 compact cached/read/write/restore LED-state validation;
- immutable generation-scoped Command targets;
- normalized Command execution statuses, optional return values, and safe
  diagnostics;
- attachment-bound Command operation ports;
- native Protocol Version 1 and Compact Serial Protocol Command adapters;
- compact logical-to-wire Command mapping below the northbound boundary;
- null-only compact Command arguments;
- no automatic Command retry after ambiguous timeout or connection loss;
- no speculative Property-cache updates after successful Commands;
- public `IRuntimeHostCommandService`;
- snapshot composition exposing Property and Command services over the same
  attachment projection;
- physical C-027 native LED toggle/return/read/restore validation;
- physical C-027 compact LED toggle/read/restore validation;
- ADR-0029 immutable observation kinds, sequences, payloads, and subscription
  options;
- authoritative initial snapshot and subscription-local sequence boundary;
- bounded independently buffered observation subscriptions;
- explicit gap termination instead of silent observation loss;
- attachment publication and ending observations;
- normalized connection-status, Property-cache, and Event observations;
- generation-bound observation delivery across attachment replacement;
- snapshot composition exposing one live-observation service over the same
  attachment projection as inventory, Property, and Command services;
- native Protocol Version 1 and Compact Serial Protocol observation integration;
- physical C-028 publication, Property, Event, and orderly-ending validation
  for both endpoint families;
- ADR-0030 versioned protobuf and ASP.NET Core gRPC mapping;
- complete snapshot and descriptor mapping;
- unary cached and authoritative Property reads and Property writes;
- unary exactly-once Command execution;
- server-streaming observation with the authoritative initial snapshot first;
- explicit observation-gap termination;
- cancellation, deadline, subscription-isolation, and graceful-shutdown
  disposal verification;
- enforced IPv4 and IPv6 loopback-only HTTP/2 hosting;
- preservation of runtime-host ownership for attachment and endpoint
  lifecycles.
- ADR-0031 northbound security boundary;
- generation-scoped northbound authorization;
- enrolled X.509 client-certificate authentication;
- system certificate-chain trust validation;
- authenticated principal projection into `HttpContext.User`;
- HTTPS-only, HTTP/2-only Kestrel hosting with TLS 1.2 or TLS 1.3;
- required client certificates at the TLS boundary;
- missing-certificate rejection before service execution;
- structurally valid but unenrolled client-certificate rejection before service
  execution;
- authenticated gRPC service execution through the existing remote adapter.
- authenticated authoritative Property execution through the mutual-TLS host;
- missing and unenrolled credential rejection before Property-service
  execution;
- physical ESP32/BME280 temperature access through the authenticated gRPC
  path;
- orderly secure-host shutdown and physical endpoint detachment;
- authenticated physical Arduino Command execution through real HTTPS/HTTP/2
  gRPC;
- missing and unenrolled credential rejection before Command-service
  execution;
- authoritative `Led.State` confirmation and restoration through secured
  Property RPCs;
- orderly secure-host shutdown and physical Arduino endpoint detachment;
- authenticated physical ESP32 server-streaming observation through real
  HTTPS/HTTP/2 gRPC;
- missing and unenrolled credential rejection before observation-subscription
  creation;
- authenticated empty initial snapshot followed by `AttachmentPublished`,
  `PropertyValueChanged`, GPIO17 `EventOccurred`, and `AttachmentEnded`;
- strictly increasing subscription-local sequences and orderly physical
  endpoint detachment.

Phase 7.7 remote API mapping is complete.
The C-034 authenticated physical northbound observation validation baseline is
complete.
The ADR-0032 controlled private-network deployment and physical validation
baseline is complete. Production promotion remains prohibited pending the
separately approved audit, governance, revocation, rotation, authorization
deployment, and operational-hardening work.

---

# Current Architecture

## Core Model

`Hase.Core` contains transport-independent identities, descriptors, paths,
quantities, units, and endpoint, instrument, property, command, and event
definitions.

## Runtime Model

`Hase.Runtime` contains runtime contexts, endpoints, instruments, properties,
property caches, connection status, command execution, protocol dispatch, and
event routing.

The physical endpoint remains authoritative. The runtime maintains a synchronized
local representation and preserves cached values during temporary disconnection.

Runtime event observers subscribe to stable `RuntimeEvent` instances. Physical
transport replacement does not replace those application-level subscriptions.

## Protocol

`Hase.Protocol` contains Protocol Version 1 messages, envelopes, codecs, and
serializers. It remains independent of TCP, mDNS, DNS-SD, ESP32, and runtime
discovery policy.

Protocol Version 1 remains separate from Compact Serial Protocol Version 1.

## Transport

`Hase.Transport` contains transport contracts, loopback transport, framed TCP
transport, transport tracing, connection invalidation, network-discovery
contracts, the IPv4 mDNS/DNS-SD browser, and the production serial byte-stream
abstraction with its `System.IO.Ports` implementation.

## Runtime Transport Integration

`Hase.Runtime.Transport` contains connection management, runtime protocol
connections, duplex sessions, protocol bindings, synchronization, recovery
supervision, health probing, notification migration, candidate verification,
discovery orchestration, endpoint attachment services, the authoritative
attachment inventory, and runtime attachment-host composition.

For compact endpoints it additionally owns compact runtime property
synchronization, compact connection coordination, compact supervision,
current-connection event authority, native runtime event routing, replacement,
resynchronization, bounded connection/bootstrap attempts, and
cancellation-aware disposal.

A COM port being present does not prove that the endpoint processor is
responsive.

## Northbound Runtime-Host Foundation

`Hase.Runtime.Northbound` contains the transport-independent application-facing
snapshot, identity, inventory-query, normalized Property service, normalized
Command service, and live-observation foundations.

The authoritative attachment inventory is projected into immutable published
endpoint snapshots containing:

- authoritative `EndpointId`;
- opaque attachment generation;
- immutable endpoint descriptor;
- captured endpoint connection status.

`RuntimeHostSnapshotProvider` combines that projection with stable
`RuntimeHostId` and `RuntimeHostApiVersion`.

Runtime-host identity resolution applies this precedence:

1. explicit configured identity;
2. previously persisted generated identity;
3. newly generated and atomically persisted identity.

`FileRuntimeHostIdentityStore` uses a strict versioned UTF-8 JSON document and
atomic non-overwriting publication. Malformed, inaccessible, incompatible, or
ambiguous persistence fails safely and is never treated as an empty store.

`RuntimeHostNorthboundSnapshotComposition` resolves identity once and composes
snapshot providers, `IRuntimeHostPropertyService`,
`IRuntimeHostCommandService`, and `RuntimeHostObservationService` over one
shared attachment-generation projection of the host-owned inventory.

The Property service exposes:

```text
GetCached(target)
ReadAsync(target)
WriteAsync(target, requestedValue)
```

Every target contains authoritative `EndpointId`, expected attachment
generation, `InstrumentId`, and `PropertyId`. Cached queries never communicate
with the endpoint. Explicit reads return only authoritative endpoint results.
Writes update the cache only after endpoint confirmation. Native and compact
operation details remain hidden behind attachment-bound adapters.

The Command service exposes:

```text
ExecuteAsync(target, argument, cancellationToken)
```

Every Command target contains authoritative `EndpointId`, expected attachment
generation, `InstrumentId`, and logical `CommandPath`. Native Protocol Version 1
passes optional arguments and return values through the normalized adapter.
Compact Commands accept only null arguments and map logical targets to compact
byte identifiers below the northbound boundary. Commands are never retried
automatically after ambiguous timeout or connection loss and never
speculatively update Property caches.

The observation service exposes an authoritative initial runtime-host snapshot,
its exact subscription-local sequence boundary, and later immutable
generation-bound observations. Independent buffers are bounded. A gap ends the
affected subscription explicitly rather than losing observations silently.
Attachment publication and ending, connection status, accepted Property-cache
updates, and transient Event occurrences share one normalized stream. Events
remain unqueued and are never replayed.

The composition, Property service, Command service, and observation service do
not own, attach, detach, replace, supervise, recover, or dispose endpoints.

## Compact Protocol

`Hase.CompactProtocol` contains the resource-constrained Compact Serial Protocol
Version 1 defined by ADR-0020 and extended for unsolicited events by ADR-0022.

Compact endpoints expose authoritative identity and a versioned descriptor
reference while the complete descriptor and compact property/event/command
mappings remain in the runtime-host repository.

One reader owns each compact connection. Correlation identifier zero is reserved
for unsolicited event notifications; correlated request/response traffic uses
nonzero identifiers.

---

# Physical Endpoints

## ESP32 / BME280 Endpoint

```text
Board       : DOIT ESP32 DEVKITC V4 / ESP32-WROOM
Endpoint ID : doit-esp32-devkitc-v4-01
TCP port    : 5000
Protocol    : HASE Protocol Version 1
Transport   : Framed TCP
Discovery   : _hase._tcp.local
IP target   : IPv4
```

The BME280 instrument exposes Temperature, Relative Humidity, and Air Pressure.
The GPIO controller exposes Boolean properties, commands, and events. Physical
GPIO17 notification was validated through the complete duplex path and after
connection recovery.

C-026 additionally validates the public northbound Property service against the
physical temperature Property. The published attachment generation scopes both
the cached query and authoritative read. Orderly host detachment ends in
`Disconnected`.

The verified IPv4 address during physical discovery remained external deployment data. The
address is dynamically discovered reachability information, not authoritative
identity.

## Arduino Uno Compact Endpoint

```text
Board              : Arduino Uno class
Endpoint ID        : arduino-uno-01
Transport          : USB serial at 115200 baud
Protocol           : Compact Serial Protocol V1
Descriptor         : arduino-uno-validation v1
Instrument         : arduino-uno-controller-01
Property           : Led.State (compact id 0x01)
Command            : Led.Toggle (compact id 0x01)
Event              : Controller.ButtonPressed (compact id 0x01)
Event value        : None
Button pin         : D7
Button electrical  : active-low INPUT_PULLUP
Debounce           : 50 ms
```

The serial connection carries binary HASE frames exclusively. Compact bootstrap
returns authoritative endpoint identity and the versioned descriptor reference.
The runtime host resolves the complete descriptor and compact mappings from its
repository.

Physical validation now covers:

- C-018 bootstrap and descriptor resolution;
- C-019 built-in LED command execution;
- C-020 Boolean LED-state synchronization into the existing runtime cache;
- C-021 automatic compact recovery after USB disconnection;
- C-022 endpoint-confirmed `Led.State` writing and confirmation reads;
- C-023 automatic Windows USB serial discovery and authoritative bootstrap;
- C-024 explicitly selected compact runtime-host attachment;
- C-025 unsolicited D7 event delivery, no replay, observer continuity, hardware
  reset recovery, and USB unplug/replug recovery;
- C-026 northbound cached LED query, authoritative read, endpoint-confirmed
  write, restoration of the original state, and orderly detachment.

### C-025 event identity

```text
Compact EventId : 0x01
InstrumentId    : arduino-uno-controller-01
EventPath       : Controller.ButtonPressed
Display name    : Button Pressed
Encoding        : None
Runtime value   : null
Timestamp       : host observation time in UTC
```

### C-025 hardware-reset recovery

With USB still connected, holding the Arduino RESET button long enough for
health probing to fail produced:

```text
Ready
-> Faulted
-> Connecting
-> bounded reconnect attempts
-> Synchronizing
-> Ready
```

The original runtime observer remained subscribed. Occurrence count remained one
after recovery, proving no replay. A new D7 press then produced occurrence two.

### C-025 USB-unplug/replug recovery

Physical USB removal produced unavailable-port reconnect failures until the selected port
returned:

```text
Ready -> Faulted
Faulted -> Connecting
Connecting -> Faulted  (selected port unavailable)
...
Connecting -> Synchronizing
Synchronizing -> Ready
```

Again, the original observer was preserved, no event was replayed, and the next
D7 press produced occurrence two.

---

# USB Serial Discovery

Windows USB serial discovery is implemented behind platform-neutral candidate,
filter, verifier, result, and orchestration contracts.

The provider uses `System.Management` and `Win32_PnPEntity`. VID, PID, product,
manufacturer, USB serial number, and COM port remain connection metadata only.

Every eligible candidate is verified sequentially through a temporary Compact
Serial Protocol connection. `CompactBootstrapResponse.EndpointId` is
authoritative, the exact descriptor reference is resolved from the host
repository, and temporary verification resources are disposed.

Discovery never attaches, publishes, replaces, or mutates runtime endpoints.

Linux USB serial discovery remains explicit backlog.

---

# Network Discovery

HASE uses mDNS with DNS-Based Service Discovery.

```text
Service type : _hase._tcp.local
Instance     : doit-esp32-devkitc-v4-01
TCP port     : 5000
```

mDNS/DNS-SD advertises reachability only. Every candidate is verified through
Protocol Version 1 `DiscoverRequest` / `DiscoverResponse`, whose returned
`EndpointId` is authoritative.

Candidates are deduplicated first by address/port and verified endpoints by
authoritative `EndpointId`. Discovery does not attach or replace runtime
endpoints automatically.

The current implementation accepts IPv4 candidates. IPv6 remains backlog.

---

# Local Endpoint Communication Lifecycle

ADR-0019 defines the HASE runtime host as owner of the complete local
communication lifecycle:

```text
Detection or configuration
    -> connection-target resolution
    -> endpoint verification or adapter probing
    -> descriptor resolution
    -> explicit attachment
    -> synchronization
    -> operation
    -> health monitoring and recovery
    -> orderly shutdown
```

Discovery and manual configuration are equal sources of connection definitions.
Detection never attaches, replaces, operates, or detaches a runtime endpoint
automatically.

C-024 applies this lifecycle to compact serial endpoints. C-025 extends the
operational portion with unsolicited compact events while retaining the same
host-owned connection, supervision, replacement, inventory, and shutdown
boundaries.

---

# Connection and Recovery

Native framed-TCP and compact serial endpoints use transport-specific
coordinators and supervisors while sharing the runtime endpoint connection-state
model.

The reconnect schedule remains:

```text
immediate
1 second
2 seconds
5 seconds
10 seconds maximum
```

Compact health probes use the owned compact connection with explicit timeout.
Probe failures invalidate and detach the unusable connection before replacement.

C-025 additionally established that each supervised compact connection/bootstrap
attempt must be bounded. This handles the case where the USB serial adapter
remains available while the endpoint processor is reset or otherwise silent.

```text
COM port present != endpoint responsive
```

With the physical defaults, compact health probes and supervised
connection/bootstrap attempts use a three-second timeout.

Successful recovery revalidates the endpoint, synchronizes readable properties,
reactivates event authority for the replacement connection, and returns the
stable runtime endpoint to `Ready`.

---

# Protocol Notifications and Diagnostics

Native Protocol Version 1 and Compact Serial Protocol Version 1 both support
unsolicited events, but their wire protocols remain separate.

For compact serial:

- correlation identifier zero is reserved for unsolicited notifications;
- one reader owns the connection;
- correlated responses and unsolicited events share that reader;
- host mappings resolve compact event IDs to runtime identities;
- only the current validated operational connection may publish;
- runtime observers survive physical connection replacement;
- there is no offline queue;
- there is no replay after reconnect;
- shutdown removes event delivery authority deterministically.

Diagnostics include connection states, health results, recovery transitions,
exchange counts, byte counts, durations, failures, replacements, and Protocol
Explorer tracing.

---

# Capabilities

- C-001 - Runtime property access through Protocol Version 1.
- C-002 - Runtime event subscription and notification routing.
- C-003 through C-014 - Physical framed TCP, Protocol Version 1 operations,
  synchronization, recovery, probing, properties, commands, duplex
  notifications, router migration, and event recovery.
- C-015 - IPv4 mDNS/DNS-SD discovery with authoritative Protocol Version 1
  endpoint verification.
- C-016 - Explicit native network endpoint attachment through the runtime-host
  lifecycle.
- C-017 - Runtime-host attachment inventory with authoritative identity,
  duplicate rejection, coordinated lifecycle ownership, and explicit detachment.
- C-018 - Physical compact serial bootstrap and host-side descriptor resolution.
- C-019 - Physical compact command execution through the Arduino Uno built-in
  LED.
- C-020 - Physical compact property reading and runtime-cache synchronization.
- C-021 - Compact serial connection supervision with health probing, bounded
  retry, replacement, resynchronization, cache preservation, and clean shutdown.
- C-022 - Endpoint-confirmed compact property writing with confirmation reads and
  runtime-cache synchronization.
- C-023 - Windows USB serial candidate discovery with metadata filtering,
  authoritative compact bootstrap verification, exact descriptor resolution,
  isolated outcomes, and unique endpoint inventory.
- C-024 - Explicitly selected compact serial endpoint attachment through the
  runtime-host inventory with independent bootstrap and operational connections,
  readiness-gated publication, synchronization, supervision, and detachment.
- C-025 - Compact Serial Event Notifications with one-reader unsolicited event
  demultiplexing, descriptor event mappings, current-connection authority,
  native runtime event routing, observer continuity, no queue/replay, bounded
  reset recovery, and physical Arduino Uno validation.
- C-026 - Generation-scoped physical northbound Property access through one
  public service for native Protocol Version 1 and Compact Serial Protocol
  endpoints.
- C-027 - Generation-scoped physical northbound Command execution through one
  public service for native Protocol Version 1 and Compact Serial Protocol
  endpoints.
- C-028 - Snapshot-first, generation-scoped physical northbound live
  observation through one public service for native Protocol Version 1 and
  Compact Serial Protocol endpoints.
- C-029 - Generation-scoped northbound authorization integrated with the
  operational gRPC boundary.
- C-030 - Enrolled X.509 client-certificate authentication, system trust
  validation, and authenticated-principal construction.
- C-031 - Mutual-TLS Kestrel runtime-host integration with authenticated gRPC
  success, TLS-boundary missing-certificate rejection, application-boundary
  unenrolled-certificate rejection, and principal projection.
- C-032 - Authenticated authoritative Property RPC through the mutual-TLS host,
  including rejection before service execution and physical ESP32/BME280
  validation.
- C-033 - Authenticated physical Arduino `Led.Toggle` through the mutual-TLS
  host, including rejection before Command execution and authoritative
  `Led.State` confirmation with restoration.
- C-034 - Authenticated physical server-streaming observation through the
  mutual-TLS host, including Property and Event delivery and orderly ending.
- ADR-0032 - Controlled private-network deployment with external credential
  provisioning, one desktop-owned heterogeneous inventory, and authenticated
  laptop snapshot, Property, Command, observation, restoration, and shutdown
  validation.
- ADR-0033 - Laptop Client application, API documentation, and tutorial.
- ADR-0034 - Production Desktop Runtime Host application.
- ADR-0035 - Interactive Desktop Runtime Host operator console.
- ADR-0036 - ByteArray values and typed Command arguments.
- ADR-0037 - Descriptor-driven Property editing.

---

# Verification Status

```text
.NET solution builds
3,643 automated tests pass
ESP32 firmware builds
Arduino Uno firmware builds
BME280 initializes
Wi-Fi connects
UTC synchronizes
TCP server listens on port 5000
mDNS advertises _hase._tcp.local
IPv4 network discovery is physically verified
C-016 native attachment and shutdown are physically verified
C-017 authoritative inventory and detachment are physically verified
C-018 compact bootstrap resolves arduino-uno-validation v1
C-019 compact LED-toggle command returns Success
C-020 synchronizes Led.State into the runtime cache
C-021 detects USB loss and returns through Synchronizing to Ready
C-021 preserves cached Led.State during fault
C-022 writes Led.State Off -> On -> Off with successful confirmation reads
C-023 discovers and authoritatively verifies the physical Arduino Uno
C-024 attaches the selected compact endpoint through the runtime-host inventory
C-024 publishes only after Ready and initial property synchronization
C-025 maps compact EventId 0x01 to Controller.ButtonPressed
C-025 physical D7 event delivery reaches the existing RuntimeEvent observer
C-025 uses host-observed UTC timestamps and null event value
C-025 suppresses stale/replaced connection events
C-025 preserves the runtime observer across connection replacement
C-025 provides no offline event queue
C-025 performs no event replay after reconnect
C-025 recovers from an Arduino hardware reset while USB remains connected
C-025 recovers from physical USB unplug/replug
C-025 bounds silent connection/bootstrap attempts and advances retry
C-025 post-recovery D7 event delivery is verified
C-025 orderly detach ends Disconnected with zero inventory and publication
Protocol Explorer C-025 exits with code 0
Stable RuntimeHostId is included in every runtime-host snapshot
Attachment generation is stable for one published entry and changes on reattach
Explicit runtime-host identity bypasses persistent storage
Persisted runtime-host identity survives composition restart
First-run identity creation is atomic and converges across concurrent callers
Malformed or incompatible identity documents fail without replacement
File-backed composition supplies the resolved identity to snapshot publication
Runtime-host snapshot composition does not dispose the attachment inventory
C-026 publishes the physical ESP32 and Arduino Uno through immutable inventory
snapshots
C-026 uses the published attachment generation for every Property target
C-026 reads the physical ESP32 temperature through cached and authoritative
northbound operations
C-026 reads, toggles, confirms, and restores the physical Arduino Uno LED state
through the same northbound Property service
C-026 orderly detachment ends both physical endpoint families in Disconnected
Protocol Explorer C-026 exits with code 0 for both endpoint families
C-027 publishes the physical ESP32 and Arduino Uno through immutable inventory
snapshots
C-027 uses the published attachment generation for every Command target
C-027 toggles and restores the physical ESP32 status LED through the
northbound Command service
C-027 passes through the native Boolean Command return value
C-027 confirms native Command results through authoritative Property reads
C-027 maps the logical Arduino Led.Toggle Command to compact CommandId 0x01
C-027 accepts a null compact argument and exposes no compact return value
C-027 confirms compact Command results through authoritative Property reads
C-027 performs no automatic Command retry or speculative Property-cache update
C-027 orderly detachment ends both physical endpoint families in Disconnected
Protocol Explorer C-027 exits with code 0 for both endpoint families
C-028 opens each observation subscription before physical attachment
C-028 initial snapshots contain zero published endpoints
C-028 observes AttachmentPublished, PropertyValueChanged, EventOccurred, and
AttachmentEnded milestones for both physical endpoint families
C-028 retains one authoritative EndpointId and attachment generation across
every milestone
C-028 native observations are delivered consecutively at sequences 1 through 4
C-028 compact observations retain additional authoritative Property-cache
updates between required milestones
C-028 physical button Events carry null values and UTC timestamps
C-028 orderly attachment ending is observed for both endpoint families
Protocol Explorer C-028 exits with code 0 for both endpoint families
ADR-0030 protobuf contracts preserve stable version 1 field and enum mappings
ADR-0030 maps snapshot, Property, Command, and observation operations explicitly
ADR-0030 loopback HTTP/2 integration passes over IPv4
ADR-0030 loopback HTTP/2 integration passes over IPv6 where supported
ADR-0030 maps observation gaps to explicit gRPC DataLoss termination
ADR-0030 propagates cancellation and deadline expiry
ADR-0030 isolates simultaneous observation subscriptions
ADR-0030 disposes active observation subscriptions during graceful host shutdown
ADR-0030 rejects wildcard and non-loopback bindings
C-032 authenticates client-01 through the mutual-TLS Property host
C-032 rejects missing and unenrolled credentials before Property execution
C-032 reads the physical ESP32 temperature authoritatively through gRPC
C-032 detaches the physical endpoint orderly to Disconnected
C-033 authenticates client-01 through the mutual-TLS Command host
C-033 rejects missing and unenrolled credentials before Command execution
C-033 toggles the physical Arduino LED through gRPC
C-033 confirms and restores Led.State through authoritative Property RPCs
C-033 detaches the physical endpoint orderly to Disconnected
ADR-0035 projects persistent Properties, Commands, and Events
ADR-0035 writes Boolean Properties through normalized operator services
ADR-0035 executes parameterless Commands without automatic retry
ADR-0035 authoritatively reconciles readable Properties after Commands
ADR-0035 retains the latest 100 completed local operator actions
ADR-0035 retains the latest 100 live endpoint Event occurrences
ADR-0035 attributes consecutive Arduino and ESP32 Events to their exact sources
ADR-0035 closes the WPF application and runtime process orderly
ADR-0036 transports opaque ByteArray Property and Command values end to end
ADR-0036 validates typed ByteArray Command arguments through the remote WPF client
ADR-0036 publishes the opt-in simulation through the normal attachment inventory
ADR-0036 preserves ByteArray Property observations without client disconnection
```

---

# Completed Objectives after ADR-0037

## ADR-0038 — Descriptor-Driven Command Argument Editing

Both Desktop Host and Client use shared descriptor-driven Boolean, numeric,
string, and byte-array Command argument editors. Local validation precedes
execution, typed arguments retain their normalized values across the remote
boundary, and state-changing Commands are never retried automatically.

## ADR-0039 — Descriptor-Driven Event Presentation

Desktop Host and Client present transient Events with exact Runtime Host,
endpoint, instrument, path, value, and timestamp attribution. Events remain
current-connection observations with no offline queue or replay.

## ADR-0040 through ADR-0042 — Structured Diagnostics

The Runtime Host gained structured operational, protocol, and byte diagnostics,
followed by separate Desktop Host and Client diagnostics windows. Both windows
support pause/resume presentation without pausing capture, bounded retention,
filtering, structured protocol-byte interpretation, and orderly disposal.
Client diagnostics remain scoped to the originating Runtime Host profile.

## ADR-0043 — Repeatable Multi-Host Deployment

Release publication, installation identity, external configuration, certificate
custody, desktop shortcuts, Runtime Host enrollment, endpoint onboarding, and
multi-host Client profiles are repeatable and update-safe. Physical validation
ran two Runtime Hosts simultaneously from one Client. Inventory, Properties,
Commands, Events, diagnostics, and reconnect behavior remained independently
host-scoped. ADR-0043 closed at 4,405 tests.

## ADR-0044 — SCPI Instrument Adapter Boundary

HASE now has a dependency-free serialized SCPI text-session boundary with
explicit ASCII framing, bounded exchanges, one ordered operation at a time,
fault-on-desynchronization behavior, deterministic concurrent disposal, and
explicit uncertain outcomes for state-changing Commands. The generic SCPI
project has no serial, KEL-103, Runtime Host, gRPC, or Client dependency.

Protocol Explorer adapts the physically characterized KEL-103 serial profile to
`IScpiByteStream`. The read-only `*IDN?` characterization now executes through
`ScpiTextSession`, requires one LF-terminated response, rejects echo and
trailing frames, preserves identity redaction and timing diagnostics, closes
normally, and releases the port for independent reuse. ADR-0044 closes its
session and characterization boundary at 4,515 tests.

## ADR-0045 — Runtime-Hosted SCPI Instrument Publication

The production Runtime Host now owns the explicitly configured KEL-103 serial
session, verification, complete synchronization, authoritative publication,
operations, supervised replacement, recovery, and disposal. The versioned
definition publishes product identity, firmware, measured voltage, measured
current, and measured power as five read-only normalized Properties.

Physical validation covered Host and Client presentation, authoritative reads,
USB reconnect, complete instrument power-cycle recovery, mixed Native/Compact/
SCPI Command and Event coexistence, simultaneous Desktop and MiniPC Runtime
Hosts, independent Client-session reconnection, sanitized diagnostics, orderly
shutdown, and deterministic port release. ADR-0045 closes at 4,772 tests.

## ADR-0048 — SCPI Protocol and Bytes Diagnostics

The dependency-free SCPI session now offers optional, failure-isolated
observation inside its serialized exchange. The production KEL-103 composition
maps observations into payload-free Protocol records and bounded exact Bytes
records under the established Runtime Host capture policy. Correlation,
sanitized terminal classification, and uncertain Command outcomes remain
explicit without retry or replay.

The Runtime Host recognizes the `ScpiText` byte family and structures printable
message bodies, Query/Command/response classification, and CR/LF terminators.
Physical passive-health and Property-read validation confirmed correlation,
endpoint scope, `0D` request termination, `0A` response termination, agreement
between raw and structured presentation, and continued `Ready` state. The
Client boundary remains unchanged. ADR-0048 closes at 5,533 tests.

---

# Architecture Decision Records

ADR-0001 through ADR-0049 are accepted.

Relevant recent decisions:

- ADR-0017 - Duplex Protocol Health Probing.
- ADR-0018 - mDNS/DNS-SD Network Endpoint Discovery.
- ADR-0019 - Local Endpoint Communication Lifecycle Ownership.
- ADR-0020 - Resource-Constrained Serial Endpoint Protocol.
- ADR-0021 - USB Serial Endpoint Discovery and Authoritative Compact
  Verification.
- ADR-0022 - Compact Serial Event Notifications.
- ADR-0023 - Northbound Runtime-Host API Boundary.
- ADR-0024 - Stable Runtime-Host Identity.
- ADR-0025 - Runtime-Host Identity Resolution.
- ADR-0026 - File-Based Runtime-Host Identity Store.
- ADR-0027 - Normalized Northbound Property Operations.
- ADR-0028 - Normalized Northbound Command Execution.
- ADR-0029 - Northbound Live Observation.
- ADR-0030 - Northbound Remote API Mapping.
- ADR-0031 - Northbound Security Boundary.
- ADR-0032 - Private-Network Runtime-Host Deployment and Credential
  Provisioning.
- ADR-0033 - Laptop Client Application, API Documentation, and Tutorial.
- ADR-0034 - Desktop Runtime Host Application.
- ADR-0035 - Interactive Operator Console.
- ADR-0036 - ByteArray Values and Typed Command Arguments.
- ADR-0037 - Descriptor-Driven Property Editing.
- ADR-0038 - Descriptor-Driven Command Argument Editing.
- ADR-0039 - Descriptor-Driven Event Presentation.
- ADR-0040 - Structured Runtime Diagnostics and Tracing.
- ADR-0041 - Desktop Diagnostics Window and Presentation Pause.
- ADR-0042 - Laptop Client Diagnostics Window and Presentation Pause.
- ADR-0043 - Repeatable Runtime-Host Deployment, Enrollment, and Multi-Host
  Client Topology.
- ADR-0044 - SCPI Instrument Adapter Boundary.
- ADR-0045 - Runtime-Hosted SCPI Instrument Publication.
- ADR-0046 - Controlled KEL-103 Operating State and Setpoints.
- ADR-0047 - Passive SCPI Instrument Health Supervision.
- ADR-0048 - SCPI Protocol and Bytes Diagnostics.
- ADR-0049 - Authorized Remote Runtime Diagnostics.
- ADR-0050 - Python Automation Boundary.
- ADR-0051 - Python Client Local Distribution and Automation Workflows.

---

# Current Limitations

The current implementation intentionally excludes:

- IPv6 discovery;
- live Added/Updated/Removed presence tracking;
- production non-loopback deployment;
- production credential provisioning, rotation, revocation, and audit;
- automatic attachment without an explicit request;
- automatic endpoint replacement;
- cross-subnet mDNS relaying;
- parallel candidate verification;
- persistent discovery results;
- Linux USB serial discovery and physical validation;
- BLE;
- formal compact-profile negotiation;
- persistent event history and replay;
- persistent operator audit history;
- operator activity and Event filtering or export;
- automatic Desktop Event-subscription recovery;
- additional compact scalar/event-value encodings;
- Tailscale runtime-host discovery;
- automatic SCPI instrument discovery and generic VISA, USBTMC, or GPIB;
- diagnostic export and offline analysis;
- remote media feedback.

---

# Immediate Next Steps

1. Select the next architectural objective through explicit approval while
   preserving the closed ADR-0046 safety, ADR-0047 through ADR-0049 diagnostic
   guarantees, and ADR-0050/ADR-0051 Python automation boundaries.
2. Keep the ADR-0032 non-loopback profile classified as controlled validation;
   do not promote it to production until audit, governance, revocation,
   rotation, authorization deployment, and operational hardening are complete.
3. Preserve the completed private-network snapshot, Property, Command,
   observation, restoration, and orderly-shutdown baselines.
4. Keep Linux USB serial discovery, IPv6 discovery, BLE, formal compact profiles,
   persistent Event history, remote lifecycle administration, and Tailscale
   runtime-host discovery as separately approved backlog.

---

# Project Principles

- Architecture changes require explicit decisions.
- Protocol Version 1 remains transport-independent.
- Compact Serial Protocol remains separate from Protocol Version 1.
- The physical endpoint is authoritative.
- Discovery metadata is not identity.
- Runtime state is synchronized from the endpoint.
- Cached values remain available during disconnection.
- One owned receive path processes each duplex or compact connection.
- Runtime event observers survive physical connection replacement.
- Offline compact events are neither queued nor replayed.
- A visible COM port is not proof of endpoint responsiveness.
- Increments remain small, buildable, and testable.
- Physical capabilities receive end-to-end validation.
- Discovered endpoints never replace active runtime endpoints automatically.
- The runtime host remains the sole owner of physical endpoint lifecycles.
- Northbound active operations are scoped to one attachment generation.
- Network reachability does not grant HASE authorization.
