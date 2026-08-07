# HASE Roadmap

## Vision

HASE is built in layers. Each completed phase becomes a stable foundation for
the following phases, and architecture changes should become increasingly rare
as the framework matures.

HASE provides transport-independent access to physical and simulated hardware
instruments through a common descriptor, runtime, protocol, and tooling model.

---

# Phase 1 - Foundation

**Status:** [Completed] Completed

Implemented:

- core domain and identity models;
- descriptor model;
- engineering quantities and units;
- property, command, and event descriptors;
- runtime context, endpoints, instruments, and properties;
- property cache and connection status;
- architecture documentation and initial ADRs;
- comprehensive unit tests.

Key outcome: HASE established a transport-independent representation of
endpoints and instruments.

---

# Phase 2 - Simulation

**Status:** [Completed] Completed

Implemented:

- `Hase.Simulation`;
- simulation host and steps;
- environment state and simulation;
- value generators and periodic waveforms;
- simulated environment sensor;
- runtime integration and tests.

Future extensions include noise, calibration, playback, JSON scenarios, and
fault injection.

---

# Phase 3 - Protocol Foundation

**Status:** [Completed] Completed

Implemented:

- protocol versions, roles, types, and correlation identifiers;
- protocol envelopes;
- binary envelope and payload serialization;
- serialization helpers;
- Variant and property-value serialization;
- descriptor serialization;
- Boolean data descriptors.

Key outcome: a deterministic binary protocol foundation independent of
transport.

---

# Phase 4 - Protocol Version 1

**Status:** [Completed] Completed

Implemented:

- discovery;
- endpoint descriptor access;
- property reads and writes;
- command execution;
- event notifications;
- String, Numeric, and Boolean data-descriptor encoding;
- full embedded descriptors;
- compact descriptor-reference architecture.

Protocol Version 1 messages are `DiscoverRequest`, `DiscoverResponse`,
`ReadEndpointDescriptorRequest`, `ReadEndpointDescriptorResponse`,
`ReadPropertyRequest`, `ReadPropertyResponse`, `WritePropertyRequest`,
`WritePropertyResponse`, `ExecuteCommandRequest`, `ExecuteCommandResponse`, and
`EventNotification`.

Protocol Version 1 is feature complete for the current Properties, Commands, and
Events contract.

---

# Phase 5 - Runtime Integration

**Status:** [Completed] Completed

Implemented:

- runtime protocol dispatcher;
- property, command, and event routing;
- loopback integration;
- Protocol Explorer;
- logical, message, and byte tracing;
- end-to-end capability scenarios.

Completion baseline:

```text
428 automated tests
```

---

# Phase 6 - Transport Infrastructure and Physical Endpoint Integration

**Status:** [Completed] Completed at the C-025 baseline

Current baseline:

```text
1,745 automated tests
.NET solution builds
ESP32 firmware builds
Arduino Uno firmware builds
Physical ESP32 endpoint verified
Physical Arduino Uno endpoint verified
IPv4 network discovery verified
Windows USB serial discovery verified
Compact serial endpoint attachment verified
Compact serial event notification verified
Arduino Uno USB-unplug/replug recovery verified
Arduino Uno hardware-reset recovery verified
```

## 6.1 Transport Abstraction

**Status:** [Completed] Completed

Implemented `Hase.Transport`, transport connection and factory contracts, duplex
connections, lifecycle states, loopback migration, and contract tests.

## 6.2 Framed TCP Transport

**Status:** [Completed] Completed

Implemented TCP options, connection factory, four-byte big-endian framing,
payload validation, connection timeouts, duplex send/receive, invalidation,
tracing, concurrency tests, and failure tests.

## 6.3 Runtime Transport Integration

**Status:** [Completed] Completed

Implemented connection management, legacy and duplex protocol connections,
protocol sessions and bindings, endpoint synchronization, and connection
coordination.

## 6.4 Automatic Reconnection

**Status:** [Completed] Completed

Implemented initial retry, transport replacement, bounded backoff, complete
resynchronization, cached-value preservation, cancellation-aware supervision,
and diagnostics.

```text
immediate
1 second
2 seconds
5 seconds
10 seconds maximum
```

## 6.5 Duplex Protocol Health Probing

**Status:** [Completed] Completed

Implemented coordinator-owned probing, explicit timeouts, silent-loss detection,
transport invalidation, recovery through the existing supervisor, one receive
path, and physical ESP32 reset validation.

Architecture: ADR-0017.

## 6.6 Runtime Event Routing and Recovery

**Status:** [Completed] Completed

Implemented unsolicited Protocol Version 1 notification routing, runtime event
observers, router migration, physical GPIO17 notification, and post-recovery
validation.

Compact Serial Protocol now reuses the same transport-independent runtime event
model after compact-specific event decoding and descriptor mapping.

## 6.7 Physical ESP32 Endpoint

**Status:** [Completed] Completed for the current endpoint contract

Hardware includes the DOIT ESP32 DEVKITC V4 / ESP32-WROOM, BME280, GPIO
controller, Wi-Fi, and framed TCP port 5000.

Physical discovery, descriptor access, property reads/writes, commands, events,
supervision, reconnect, probing, resynchronization, and event recovery are
verified. Capabilities C-003 through C-014 are complete.

## 6.8 Network Endpoint Discovery

**Status:** [Completed] Implemented and physically verified for IPv4

```text
Technology : mDNS/DNS-SD
Service    : _hase._tcp.local
Instance   : doit-esp32-devkitc-v4-01
TCP port   : 5000
```

Implemented:

- platform-neutral network candidate/browser contracts;
- `MdnsNetworkEndpointBrowser`;
- cancellation-aware browsing and IPv4 filtering;
- candidate deduplication by address and port;
- Protocol Version 1 candidate verification;
- timeout, unreachable, non-HASE, and invalid-response isolation;
- authoritative `EndpointId` extraction;
- verified endpoint deduplication by `EndpointId`;
- discovery orchestration;
- Protocol Explorer network-discovery scenario;
- ESP32 mDNS advertiser;
- clean Ctrl+C cancellation.

Constraints:

- mDNS advertises reachability, not identity;
- `DiscoverResponse.EndpointId` is authoritative;
- Protocol Version 1 remains unchanged;
- candidate failures remain isolated;
- discovery never attaches or replaces runtime endpoints automatically;
- same-identity reappearance is not emitted twice in one discovery session;
- live presence tracking remains backlog;
- IPv6 remains backlog.

Architecture: ADR-0018.

## 6.9 Explicit Endpoint Attachment and Lifecycle Ownership

**Status:** [Completed] Implemented and physically verified for native framed TCP

Architecture: ADR-0019.

The HASE runtime host owns the complete local lifecycle:

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

Discovery and manual configuration are equal connection-definition sources.
Detection never attaches or replaces a runtime endpoint automatically.

Implemented C-016 native bootstrap, operational revalidation,
readiness-gated publication, recovery ownership, and orderly shutdown.

## 6.10 Runtime-host Attachment Inventory

**Status:** [Completed] Implemented and physically verified

C-017 adds the host-owned authoritative attachment inventory.

Implemented:

- immutable attachment inventory entries;
- authoritative identity from attached `RuntimeEndpoint`;
- attach, find, snapshot list, detach, and asynchronous disposal;
- duplicate identity rejection without automatic replacement;
- cleanup of rejected sessions;
- deterministic coordination of attachment and disposal;
- runtime attachment-host composition;
- native framed-TCP physical validation.

## 6.11 Resource-Constrained USB Serial Endpoints

**Status:** [Completed] Implemented and physically verified for the current
Arduino Uno compact endpoint contract

Architecture: ADR-0020 and ADR-0022.

Implemented:

- production USB serial byte transport;
- Compact Serial Protocol Version 1 framing and CRC;
- authoritative compact bootstrap identity;
- versioned host descriptor repository resolution;
- compact command execution;
- descriptor-side compact property mappings;
- Boolean property encoding and decoding;
- compact property reads and writes;
- runtime property-cache synchronization;
- compact connection ownership and coordinated replacement;
- recurring compact protocol health probes;
- automatic retry, replacement, and resynchronization;
- immediate, 1-second, 2-second, 5-second, and bounded 10-second reconnect
  delays;
- cache preservation while disconnected;
- cancellation-aware supervision and disposal;
- endpoint-confirmed property writes;
- compact unsolicited event notifications;
- one-reader correlated-response and unsolicited-event demultiplexing;
- host-side compact event mappings;
- current-connection-authoritative event publication;
- native runtime event routing;
- runtime observer continuity across replacement;
- no offline event queue and no replay;
- bounded connection/bootstrap attempts for a present-but-silent serial endpoint;
- deterministic event-delivery shutdown;
- Arduino Uno D7 event firmware.

Physical C-018 through C-025 are complete.

## 6.12 USB Serial Endpoint Discovery and Verification

**Status:** [Completed] Implemented and physically verified on Windows

Architecture: ADR-0021.

Implemented:

- platform-neutral USB serial candidate/filter/verifier/result contracts;
- Windows enumeration through `System.Management` and `Win32_PnPEntity`;
- optional VID/PID/port/product/manufacturer/serial filtering;
- normalized connection-target deduplication;
- sequential candidate verification with explicit timeouts;
- temporary `System.IO.Ports` connection ownership;
- authoritative identity from `CompactBootstrapResponse.EndpointId`;
- exact versioned descriptor resolution;
- isolated expected candidate failures;
- caller-cancellation propagation;
- unique verified inventory deduplicated by authoritative `EndpointId`;
- production Windows composition;
- Protocol Explorer C-023;
- no automatic runtime attachment.

Linux USB serial discovery remains explicit backlog.

## 6.13 Compact Serial Endpoint Attachment

**Status:** [Completed] Implemented and physically verified on Windows

Capability C-024 extends the runtime-host-owned lifecycle and authoritative
inventory to compact serial endpoints.

Implemented:

- configured and discovery-derived serial definitions converging on one
  attachment service;
- host repository compact endpoint definitions;
- temporary authoritative attachment bootstrap;
- independent operational compact connection;
- authoritative endpoint and descriptor revalidation;
- initial readable-property synchronization before `Ready`;
- readiness-gated publication;
- shared native and compact attachment ownership;
- failed-attachment cleanup;
- recurring compact supervision and recovery;
- duplicate authoritative-identity rejection;
- explicit inventory detachment and orderly shutdown;
- production compact runtime-host composition;
- Protocol Explorer C-024 physical validation.

The discovery-verification, attachment-bootstrap, and operational connections are
distinct ownership scopes.

## 6.14 Compact Serial Event Notifications

**Status:** [Completed] Implemented, automated, and physically verified on
Windows

Architecture: ADR-0022 - Compact Serial Event Notifications.

Capability C-025 adds unsolicited compact endpoint-to-host event delivery without
merging Compact Serial Protocol with Protocol Version 1.

Implemented:

- `EventNotification` compact message type `0x09`;
- correlation identifier zero reserved for unsolicited notifications;
- nonzero correlation identifiers retained for request/response exchanges;
- one compact connection receive loop owning all incoming frames;
- event/responses demultiplexed by correlation semantics;
- malformed correlation/message combinations rejected;
- compact event descriptor mappings from EventId to `InstrumentId` and
  `EventPath`;
- stable mapped-event source across physical connection replacement;
- explicit current-connection event authority;
- event delivery only after operational validation;
- stale and replaced connections unable to publish;
- compact runtime routing into the existing `RuntimeEvent`;
- host-observed UTC timestamps for compact events;
- persistent runtime observer subscriptions across connection replacement;
- no offline event queue;
- no replay after reconnect;
- deterministic shutdown;
- Arduino Uno D7 active-low `INPUT_PULLUP` event publisher with 50 ms debounce;
- EventId `0x01` mapped to
  `arduino-uno-controller-01 / Controller.ButtonPressed`;
- `CompactEventValueEncoding.None` and runtime value `null`;
- Protocol Explorer `c025`;
- automated pre-Ready, stale-connection, replacement, no-replay, and shutdown
  lifecycle coverage;
- bounded supervised connection/bootstrap attempts to recover from a silent MCU
  while its serial adapter remains present.

Physical validation confirmed basic delivery:

```text
Candidate port         : External runtime-selected port
VID                    : 0x2341
PID                    : 0x0043
Product                : Arduino Uno
Authoritative endpoint : arduino-uno-01
Descriptor reference   : arduino-uno-validation v1
Connection state       : Ready
Runtime event          : Controller.ButtonPressed
Value                  : null
Timestamp              : UTC
```

Hardware-reset recovery with USB still connected confirmed:

```text
Ready
-> Faulted
-> Connecting
-> bounded silent attempts
-> Synchronizing
-> Ready

Observer subscription      : Preserved
Occurrence count after Ready: 1
Replay after reset         : None
Post-reset D7 press         : occurrence 2
```

USB unplug/replug regression confirmed:

```text
Ready -> Faulted
Faulted -> Connecting
Connecting -> Faulted  (selected port unavailable)
...
Connecting -> Synchronizing
Synchronizing -> Ready

Observer subscription      : Preserved
Occurrence count after Ready: 1
Replay after recovery      : None
Post-recovery D7 press      : occurrence 2
```

Final C-025 automated baseline:

```text
1,745 tests pass
```

## 6.15 Phase 6 Closure

**Status:** [Completed] Completed

Phase 6 closes at the verified C-025 baseline.

The following optional extensions are deferred and do not block Phase 6
completion:

- Wi-Fi interruption and re-advertisement validation;
- bounded parallel USB serial candidate verification;
- Linux USB serial discovery and physical validation;
- formal compact-profile compatibility;
- IPv6 discovery;
- BLE;
- additional compact operations and value encodings.

Each deferred item requires a separately approved increment.

---

# Phase 7 - Northbound Runtime-Host API

**Status:** [Active] Active - architecture accepted

Architecture: ADR-0023 - Northbound Runtime-Host API Boundary.

Phase 7 allows local and remote applications to use the runtime model owned by a
HASE runtime host. The runtime host continues to own every physical endpoint
connection, southbound protocol or adapter session, synchronization service,
recovery supervisor, notification route, and attachment lifetime.

The approved dependency direction is:

```text
Local or remote application
    -> northbound runtime-host API
        -> runtime-host application services
            -> authoritative attachment inventory
            -> runtime model
            -> host-owned operation routing
                -> native or compact endpoint integration
```

## 7.1 Northbound Snapshot and Identity Contracts

**Status:** [Completed] Completed

Implemented:

- dedicated `Hase.Runtime.Northbound` project;
- stable authoritative `RuntimeHostId`;
- `RuntimeHostApiVersion`;
- immutable runtime-host snapshots;
- immutable published endpoint attachment snapshots;
- authoritative `EndpointId`;
- opaque attachment generation;
- endpoint descriptor and captured connection status;
- stable generation for one published inventory entry;
- new generation after reattachment, including reattachment with the same
  endpoint identity;
- ADR-0024 stable runtime-host identity;
- ADR-0025 identity-resolution precedence;
- explicit, persisted, and generated-and-persisted resolution origins;
- canonical GUID-based generated identities;
- ADR-0026 file-based identity persistence;
- strict versioned UTF-8 JSON identity documents;
- atomic non-overwriting first-run publication;
- concurrent first-run convergence;
- file-backed snapshot composition over the host-owned inventory.

Normalized Properties and active Property targeting are completed in Phase 7.3.
Normalized Commands are completed in Phase 7.4. Events and live observation
remain in the dedicated application-service increments below.

## 7.2 Runtime-Host Inventory Query Service

**Status:** [Completed] Completed

Implemented:

- immutable list projection sourced only from the authoritative runtime-host
  attachment inventory;
- authoritative `EndpointId` lookup;
- exclusion of discovery candidates, configured connection definitions, staged
  endpoints, and failed attachment attempts;
- per-entry attachment-generation retention;
- generation retirement when an inventory entry ends;
- runtime-host snapshot capture with stable resolved host identity;
- composition that leaves inventory and endpoint lifecycle ownership with the
  runtime host.

## 7.3 Normalized Property Operations

**Status:** [Completed] Implemented, automated, and physically verified

Architecture: ADR-0027 - Normalized Northbound Property Operations.

Implemented:

- immutable generation-scoped `RuntimeHostPropertyTarget`;
- immutable `PublishedRuntimePropertySnapshot`;
- separate cached queries, authoritative reads, and endpoint-confirmed writes;
- normalized success and failure statuses;
- descriptor-based requested-value validation;
- one shared attachment-generation authority for snapshots and operations;
- attachment-bound Property operation ports retained by host-owned sessions;
- native Protocol Version 1 Property adapter;
- Compact Serial Protocol Property adapter;
- compact logical-to-wire reverse lookup hidden below the application boundary;
- cache updates only from authoritative endpoint results;
- public `IRuntimeHostPropertyService`;
- composition over the exact inventory projection used by published snapshots;
- automated common-contract validation for native and compact endpoints;
- Protocol Explorer C-026.

Physical C-026 validation confirmed:

```text
ESP32:
    published Ready attachment
    -> cached temperature
    -> authoritative temperature read
    -> orderly detachment
    -> Disconnected

Arduino Uno:
    published Ready attachment
    -> cached LED state
    -> authoritative LED-state read
    -> endpoint-confirmed toggled write
    -> restoration of original LED state
    -> orderly detachment
    -> Disconnected
```

The runtime host retains all discovery, attachment, supervision, recovery,
detachment, and disposal ownership.

## 7.4 Normalized Command Execution

**Status:** [Completed] Implemented, automated, and physically verified

Architecture: ADR-0028 - Normalized Northbound Command Execution.

Implemented:

- public `IRuntimeHostCommandService`;
- immutable generation-scoped `RuntimeHostCommandTarget`;
- logical `InstrumentId` and `CommandPath` targeting;
- normalized success and failure statuses;
- optional successful return value and optional safe diagnostic;
- cancellation through `OperationCanceledException`;
- one shared attachment-generation authority for snapshots and operations;
- attachment-bound Command operation ports retained by host-owned sessions;
- native Protocol Version 1 Command adapter with optional argument and return
  value pass-through;
- Compact Serial Protocol Command adapter accepting only null arguments;
- compact logical-to-wire Command reverse lookup hidden below the application
  boundary;
- no automatic retry after ambiguous timeout or connection loss;
- no speculative Property-cache update after successful Commands;
- composition over the exact inventory projection used by published snapshots
  and Property operations;
- automated common-contract validation for native and compact endpoints;
- Protocol Explorer C-027.

Physical C-027 validation confirmed:

```text
ESP32:
    published Ready attachment
    -> authoritative original LED-state read
    -> northbound toggle Command
    -> Boolean return value matches authoritative Property read
    -> northbound restoration Command
    -> Boolean return value matches restored authoritative Property read
    -> orderly detachment
    -> Disconnected

Arduino Uno:
    published Ready attachment
    -> authoritative original LED-state read
    -> logical Led.Toggle mapped to compact CommandId 0x01
    -> successful Command with no return value
    -> authoritative toggled LED-state read
    -> successful restoration Command with no return value
    -> authoritative restored LED-state read
    -> orderly detachment
    -> Disconnected
```

The runtime host retains all discovery, attachment, supervision, recovery,
replacement, detachment, and disposal ownership.

## 7.5 Lifecycle, Property, and Event Observation

**Status:** [Completed] Implemented, automated, and physically verified

Architecture: ADR-0029 - Northbound Live Observation.

Implemented:

- immutable observation kinds, subscription-local sequences, payloads, and
  subscription options;
- authoritative initial runtime-host snapshot and exact sequence boundary;
- bounded independently buffered subscriptions;
- explicit observation-gap termination instead of silent loss;
- attachment publication and ending observations;
- normalized connection-status changes;
- authoritative Property-cache update observations;
- transient Event occurrence observations with no offline queue or replay;
- opaque attachment-generation binding on every observation;
- race-free attachment projection shared with snapshots and operations;
- deterministic subscription disposal without endpoint lifecycle ownership;
- native Protocol Version 1 and Compact Serial Protocol integration through the
  same observation service;
- Protocol Explorer observation formatting for every normalized kind;
- Protocol Explorer C-028.

Physical C-028 validation confirmed:

```text
ESP32:
    empty initial snapshot
    -> AttachmentPublished, sequence 1
    -> PropertyValueChanged, sequence 2
    -> EventOccurred from GPIO17, sequence 3
    -> AttachmentEnded, sequence 4
    -> exit code 0

Arduino Uno:
    empty initial snapshot
    -> AttachmentPublished, sequence 1
    -> PropertyValueChanged, sequence 2
    -> additional retained Property cache updates
    -> EventOccurred from D7, sequence 5
    -> AttachmentEnded, sequence 6
    -> exit code 0
```

Both physical runs retained one authoritative endpoint identity and attachment
generation across all required milestones. Event values were null and Event
timestamps were expressed in UTC. The runtime host retained attachment,
connection, synchronization, recovery, detachment, and disposal ownership.

## 7.6 Unified Native and Compact Validation

**Status:** [Completed] Property, Command, and observation services completed

Normalized Property, Command, and live-observation services are automated and
physically validated for native Protocol Version 1 and Compact Serial Protocol
endpoints through the same in-process northbound application-service boundary.

## 7.7 Remote API Technology

**Status:** [Completed] Completed under ADR-0030

Implemented ASP.NET Core gRPC over HTTP/2 with:

- a versioned protobuf package and generated CLR service types;
- unary snapshot, cached Property read, authoritative Property read, Property
  write, and Command execution operations;
- server-streaming live observation;
- the authoritative initial snapshot and sequence boundary as the first stream
  message;
- explicit mapping for every normalized observation kind and payload;
- bounded application-subscription delivery without an adapter queue;
- explicit observation-gap termination;
- cancellation and deadline propagation;
- deterministic subscription disposal during cancellation, deadline expiry,
  and graceful host shutdown;
- independent simultaneous subscriptions;
- enforced IPv4 and IPv6 loopback-only binding;
- real HTTP/2 process integration through generated clients.

The completed remote mapping adapts to the application services and does not
become a dependency of the runtime core. Its current verified baseline is
2,850 passing automated tests.

Non-loopback exposure is not part of Phase 7.7.

## 7.8 Security and Remote Exposure

**Status:** [Completed] Automated and physical security validation completed
through C-034

Architecture: ADR-0031 - Northbound Security Boundary.

Implemented:

- generation-scoped northbound authorization;
- enrolled X.509 client-certificate authentication;
- system certificate-chain trust validation;
- authenticated HASE principal construction;
- principal projection into `HttpContext.User`;
- HTTPS-only, HTTP/2-only Kestrel hosting;
- TLS 1.2 or TLS 1.3;
- required client certificates;
- authenticated gRPC service execution;
- TLS-boundary rejection when no client certificate is presented;
- HASE authentication rejection for a structurally valid but unenrolled client
  certificate;
- loopback-only automated integration;
- authenticated authoritative Property execution through real HTTPS/HTTP/2
  gRPC;
- missing and unenrolled credential rejection before Property-service
  execution;
- physical ESP32/BME280 temperature access through the authenticated gRPC
  path;
- orderly secure-host shutdown and physical endpoint detachment;
- authenticated physical Arduino Command execution through real HTTPS/HTTP/2
  gRPC;
- missing and unenrolled credential rejection before Command-service execution;
- authoritative `Led.State` confirmation and restoration through the secured
  Property RPC path;
- authenticated physical ESP32 server-streaming observation through real
  HTTPS/HTTP/2 gRPC;
- missing and unenrolled credential rejection before observation-subscription
  creation;
- authenticated empty initial snapshot followed by `AttachmentPublished`,
  `PropertyValueChanged`, GPIO17 `EventOccurred`, and `AttachmentEnded`;
- strictly increasing subscription-local sequences and orderly physical
  endpoint detachment.

Network reachability does not grant HASE authority. Production non-loopback
deployment, credential rotation, revocation operations, governance, and audit
remain separate backlog.

## 7.9 Controlled Private-Network Deployment

**Status:** [Completed] Implemented and physically validated under ADR-0032

Implemented:

- explicit non-loopback, non-wildcard, fixed-port HTTPS/HTTP/2 binding;
- external versioned desktop and laptop configuration;
- operating-system certificate-store private-key custody;
- server IP-identity validation plus exact client-side certificate pinning;
- system-trusted and explicitly enrolled client-certificate authentication;
- one desktop-owned heterogeneous attachment inventory;
- physical native-network and compact-serial endpoint publication;
- authenticated laptop snapshot and authoritative Property access;
- authenticated Arduino Command execution, confirmation, and restoration;
- authenticated Property and physical Event observation;
- explicit stream cancellation and orderly shutdown;
- provisioning and installation tooling that keeps machine-specific deployment
  data outside the repository.

Verified baseline:

```text
3,643 automated tests pass
Controlled two-computer physical validation succeeds
```

The profile is approved for controlled private-network validation only.
Production promotion remains blocked by the outstanding ADR-0031 audit,
resource-governance, authorization-deployment, revocation, rotation, and
operational-hardening requirements.

## 7.10 ByteArray Values and Typed Command Arguments

**Status:** [Completed] Implemented, automated, and remotely validated under
ADR-0036

Implemented:

- immutable content-equal `ByteArrayValue`;
- `ByteArrayDataDescriptor`;
- Protocol Version 1 Variant discriminator `0x06`;
- Property descriptor discriminator `0x04`;
- backward-compatible typed Command argument descriptor extensions;
- runtime argument validation;
- Property, Command, protobuf, gRPC, observation, and client mappings;
- hexadecimal WPF argument entry and ByteArray value display;
- command-line WPF client configuration;
- generic in-process endpoint attachment through the normal inventory;
- opt-in `simulation-byte-buffer-validation` Desktop Host endpoint;
- exact replacement, return-value, cache-update, and observation validation.

Verified baseline:

```text
3,573 automated tests pass
Controlled desktop-to-laptop ByteArray validation succeeds
```

## 7.11 Future Management API

**Status:** [Backlog] Not part of the first northbound capability

Possible later management operations include discovery, connection
configuration, attachment, detachment, replacement policy, descriptor-repository
administration, persistent host configuration, and host shutdown.

Operational access and lifecycle administration remain separate authorization
surfaces.

## 7.12 Application and Tooling Expansion

**Status:** [Completed] Completed through ADR-0037

Implemented:

- production WPF Laptop Client with authenticated private-network access;
- production Windows Desktop Runtime Host application;
- persistent endpoint, instrument, Property, Command, and Event projection;
- live Property updates and change visualization;
- descriptor-driven Boolean, Numeric, String, and ByteArray Property writes;
- explicit parameterless Command execution;
- authoritative Property reconciliation after Commands;
- bounded local operator activity;
- bounded live endpoint Event occurrences with exact source attribution;
- API documentation and Laptop Client tutorial; and
- orderly client, Event subscription, runtime host, and process shutdown.

Validated baseline:

```text
3,643 automated tests pass
ESP32 and Arduino operator-console validation succeeds
Laptop Client interoperability succeeds
```

ADR-0036 additionally completes immutable opaque ByteArray values, typed
Command arguments, hexadecimal WPF argument editing, explicit protobuf/gRPC
mapping, ByteArray Property observation, and an opt-in in-process validation
endpoint using the normal attachment inventory.

ADR-0037 additionally completes shared descriptor-driven Boolean, Numeric,
String, and ByteArray Property editors in the Laptop Client and Desktop Runtime
Host, including local and remote validation against the writable multi-type
simulation.

Deferred application work includes richer editor controls, filtering, export,
persistent histories, discovery controls, configuration editing, and lifecycle
administration.

Architecture constraints:

- discovery never automatically replaces an existing runtime endpoint;
- applications never acquire or share the physical endpoint connection;
- the runtime host remains authoritative for attachment lifecycle ownership.

---

# Future Transport Work

Possible transports and extensions include:

- IPv6 mDNS/DNS-SD;
- Linux USB serial discovery;
- additional USB serial devices and metadata filters;
- BLE;
- MQTT;
- remote access;
- Tailscale-assisted runtime-host discovery;
- gateway transports;
- additional compact scalar and event-value encodings;
- formal compact profiles.

Compact event notification itself is complete through C-025.

Transport implementations remain below the relevant protocol boundary.

---

# Future Protocol Work

Protocol Version 1 is frozen for the current endpoint contract.

Authentication, authorization, encryption, capability negotiation, bulk
operations, streaming, descriptor negotiation, compact profiles, additional
compact value encodings, and gateway routing require explicit future decisions.

Compact Serial Protocol remains separate from Protocol Version 1.

---

# Future Runtime and Simulation Work

Possible runtime work includes:

- multiple attached endpoints;
- replacement policy;
- persistence;
- offline inventory;
- calibration;
- settings and EEPROM workflows;
- scheduling;
- optional persistent event history.

Persistent event history must not be confused with C-025 delivery semantics:
C-025 intentionally provides no offline queue and no reconnect replay.

Possible simulation work includes noise, drift, calibration offsets, device and
network failures, playback, scripted scenarios, and multi-endpoint simulation.

---

# Documentation Roadmap

Current documentation includes:

- `Architecture.md`;
- `RuntimeArchitecture.md`;
- `RuntimeComponentModel.md`;
- `SerializationModel.md`;
- `ProjectStatus.md`;
- `Roadmap.md`;
- `C-023-USB-Serial-Endpoint-Discovery.md`;
- `C-024-Compact-Serial-Endpoint-Attachment.md`;
- `C-025-Compact-Serial-Event-Notifications.md`;
- `C-028-Northbound-Live-Observation.md`;
- `C-031-Mutual-TLS-Runtime-Host-Integration.md`;
- `C-032-Authenticated-Physical-Northbound-gRPC-Validation.md`;
- `C-033-Authenticated-Physical-Northbound-Command-Validation.md`;
- `C-034-Authenticated-Physical-Northbound-Observation-Validation.md`;
- ADR-0001 through ADR-0036.

Next:

1. Keep physical capabilities C-015 through C-034 and their validation
   baselines current.
2. Keep Phase 6 closure and its deferred optional extensions explicit.
3. Keep the Phase 7 northbound API boundary, identity foundation, normalized
   services, gRPC mapping, security boundary, and controlled private-network
   deployment and applications aligned with ADR-0023 through ADR-0036.
4. Keep attachment generation separate from authoritative endpoint identity.
5. Keep operational access separate from lifecycle administration.
6. Keep compact current-connection Event authority, no-queue, and no-replay
   semantics explicit.
7. Record future deployment, credential-lifecycle, audit, management,
   compact-profile,
   discovery-concurrency, or Event-history architecture changes in ADRs.

---

# Current Priorities

1. Keep the ADR-0032 non-loopback profile classified as controlled validation
   until production credential lifecycle, revocation, governance, audit, and
   operational-hardening behavior are separately approved and implemented.
2. Preserve the completed private-network snapshot, Property, Command,
   observation, restoration, and orderly-shutdown baselines.
3. Keep Linux USB serial discovery, IPv6 discovery, BLE, formal compact profiles,
   persistent Event history, lifecycle administration, and Tailscale host
   discovery as separately approved backlog.

---

# Phase 6 Completion Criteria

Already achieved:

- transport abstraction and framed TCP;
- duplex protocol sessions;
- automatic recovery and health probing;
- physical endpoint integration;
- physical properties, commands, and events;
- native Protocol Version 1 event recovery;
- IPv4 network discovery;
- authoritative endpoint verification;
- C-016 explicit endpoint attachment and lifecycle ownership;
- C-017 authoritative runtime-host attachment inventory;
- USB serial transport and Compact Serial Protocol Version 1;
- C-018 compact bootstrap and descriptor resolution;
- C-019 compact command execution;
- C-020 compact property reading and runtime-cache synchronization;
- C-021 compact serial supervision, probing, bounded recovery,
  resynchronization, cache preservation, and shutdown;
- C-022 compact property writing and endpoint confirmation;
- C-023 Windows USB serial discovery and authoritative compact verification;
- C-024 explicitly selected compact endpoint attachment through the runtime-host
  inventory;
- C-025 unsolicited compact event notifications;
- C-025 one-reader response/event demultiplexing;
- C-025 host-side compact event mapping;
- C-025 current-connection event authority and stale-connection suppression;
- C-025 native runtime observer continuity;
- C-025 no offline queue and no replay;
- C-025 Arduino Uno physical D7 event delivery;
- C-025 bounded recovery from a present-but-silent reset endpoint;
- C-025 physical hardware-reset recovery;
- C-025 physical USB-unplug/replug regression.

Phase 6 is complete. Optional extensions remain backlog:

- IPv6 discovery;
- BLE;
- additional compact operations;
- Linux USB serial discovery and physical validation;
- formal compact-profile compatibility.

The northbound runtime-host API begins in Phase 7 under ADR-0023.

---

# Phase 7 — Recent Architectural Objectives

## Completed

- ADR-0037 — Descriptor-Driven Property Editing.
- ADR-0038 — Descriptor-Driven Command Argument Editing.
- ADR-0039 — Descriptor-Driven Event Presentation.
- ADR-0040 — Structured Runtime Diagnostics and Tracing.
- ADR-0041 — Desktop Diagnostics Window and Presentation Pause.
- ADR-0042 — Laptop Client Diagnostics Window and Presentation Pause.
- ADR-0043 — Repeatable Runtime-Host Deployment, Enrollment, and Multi-Host
  Client Topology — complete at 4,405 tests after simultaneous Desktop and
  MiniPC Runtime Host validation from one laptop Client.
- ADR-0044 — SCPI Instrument Adapter Boundary — complete at 4,515 tests.
- ADR-0045 — Runtime-Hosted SCPI Instrument Publication — complete at 4,772
  tests after physical recovery and simultaneous multi-host validation.

ADR-0042 closed at 4,017 automated tests with all thirty-seven combined
physical ESP32 and Arduino Uno validation checks passing without deviation.

## Completed ADR-0043 increments

1. 43A — Deployment and Multi-Host Configuration Contracts — complete.
2. 43B — Release Publication and Runtime Host Launcher — complete.
3. 43C — Client Release Publication and Launcher — complete.
4. 43D — Multi-Host Client Session Core — complete.
5. 43E — Multi-Host WPF Presentation — complete.
6. 43F — Client Enrollment Recipe — complete.
7. 43G — Runtime Host and Endpoint Onboarding Recipe — complete.
8. 43H — Combined Multi-Host Validation and Closure — complete.

ADR-0043 closed at 4,405 passing tests. The Desktop and MiniPC Runtime Hosts ran
simultaneously, the laptop Client maintained both authenticated sessions, and
host-scoped inventory, Property, Command, Event, diagnostics, and independent
disconnect/reconnect behavior passed physical validation. The existing mutual-
TLS, certificate-pinning, explicit endpoint-attachment, and runtime-host
lifecycle-ownership boundaries remain unchanged. Tailscale remains reachability
only.

## Completed objective — ADR-0044 SCPI Instrument Adapter Boundary

**Status:** [Completed boundary] SCPI session and read-only KEL-103
characterization migration complete through 44B5C

ADR-0044 adds the first non-HASE southbound instrument adapter without changing
the normalized runtime or northbound application boundaries.

The first physical target is the KORAD KEL-103 programmable DC electronic load
over USB virtual serial. Characterization physically verified 115200 baud,
8 data bits, no parity, one stop bit, no flow control, ASCII text, CR command
termination, LF response termination, no echo, bounded response collection,
sanitized identity verification, and deterministic port release.

The first physical attempt also established that Windows
`SerialPort.BaseStream.ReadAsync` cancellation is not a sufficient timeout
boundary. The corrected utility races the physical read against an independent
timer and disposes the owned port if the timer wins. Automated coverage includes
a read that deliberately ignores cancellation.

The accepted boundary requires:

- SCPI syntax, serial framing, query matching, parsing, and device-specific
  errors to remain below the normalized runtime model;
- explicit host-side endpoint definitions and attachment;
- Runtime Host ownership of verification, synchronization, supervision,
  recovery, and disposal;
- one serialized command/query pipeline per physical session;
- no automatic retry of mutating operations;
- publication only after authoritative verification and initial
  synchronization;
- reuse of existing descriptor-driven Properties and Commands;
- no SCPI-specific northbound contract; and
- no arbitrary operator-entered SCPI console.

Completed increments:

1. 44A1 — Architecture decision — complete.
2. 44A2 — Read-only KEL-103 serial characterization utility — complete.
3. 44A3 — Physical protocol characterization documentation — complete.
4. 44B — Serialized SCPI text-session core and KEL-103 characterization
   migration — complete.

Runtime publication and capability mapping continued under ADR-0045 rather
than extending the completed adapter-boundary decision.

Current verified baseline:

```text
4,515 automated tests pass
KEL-103 read-only identity characterization succeeds through ScpiTextSession
The serial port is released for immediate reuse
```

Initially deferred KEL-103 features include saved configuration recall, external
triggering, LIST, OCP, OPP, battery, dynamic, pulse, and flip modes. Generic
VISA, USBTMC, GPIB, automatic instrument discovery, and a public instrument-
definition repository also remain later work.

## Completed objective — ADR-0045 Runtime-Hosted SCPI Instrument Publication

**Status:** [Completed] Implemented, automated, and physically validated

ADR-0045 publishes explicitly configured SCPI instruments through the existing
normalized endpoint, instrument, Property, attachment-generation, diagnostics,
Runtime Host, and Client boundaries. `Hase.Scpi` remains transport independent.

The completed KEL-103 slice is read-only. Product identity, firmware, measured
voltage, measured current, and measured power were separately characterized and
published as normalized Properties. No writable Property, Command, automatic
discovery, arbitrary SCPI console, or raw SCPI diagnostics are exposed.

Completed increments:

1. 45A — Decision and read-only safety boundary — complete.
2. 45B — Reusable serial-to-SCPI bridge — complete.
3. 45C — Versioned KEL-103 identity definition — complete.
4. 45D — Read-only measurement characterization — complete.
5. 45E — Normalized KEL-103 runtime adapter — complete.
6. 45F — Attachment, supervision, and synchronization — complete.
7. 45G — External Runtime Host profile integration — complete.
8. 45H — Desktop Host and Client presentation validation — complete.
9. 45I — Physical recovery and multi-host validation — complete.
10. 45J — Documentation and closure — complete.

Current baseline:

```text
4,772 automated tests pass
KEL-103 USB and complete power-cycle recovery verified
Native, Compact, and SCPI endpoints operated concurrently
Desktop and MiniPC Runtime Hosts operated simultaneously from one Client
Operational diagnostics remained sanitized
Runtime Hosts and Client stopped; serial port independently reopened
```

## Completed objective — ADR-0046 Controlled KEL-103 Operating State and Setpoints

**Status:** [Completed] Implemented, automated, physically validated, and closed
at 5,479 tests

ADR-0046 extends the completed read-only KEL-103 attachment with authoritative
display of CC, CV, CR, CW, and SHORT mode; input ON/OFF state; and voltage,
current, resistance, and power targets. State changes remain device-specific
below the unchanged normalized Runtime Host and Client contracts.

Setpoints use read/write Properties. Mode and input behavior use explicit
Commands. Mode and setpoint changes require authoritative input OFF. Generic
activation rejects SHORT; a separate short-circuit activation Command requires
explicit Boolean confirmation. Every mutation is transmitted once, read back,
never retried, and never replayed during recovery.

Completed and planned increments:

1. 46A — Decision, safety model, and characterization plan — complete.
2. 46B — Read-only mode, input-state, and setpoint characterization — complete
   at 4,854 tests.
3. 46C — Read-only upper/lower-limit characterization — complete at 4,905
   tests.
4. 46D — Input-OFF mode-selection characterization and restoration — complete
   at 4,937 tests.
5. 46E — Input-OFF setpoint-write characterization and restoration — complete
   at 5,000 tests.
6. 46F — Versioned state and controlled-capability definitions — complete at
   5,018 tests.
7. 46G — Runtime reads, writes, Commands, readback, and uncertain outcomes —
   complete at 5,283 tests.
8. 46H — Hosting, recovery, diagnostics, Host, and Client integration — complete
   at 5,285 tests.
9. 46I — Controlled activation, deactivation, SHORT, recovery, and presentation
   validation — complete at 5,479 tests.
10. 46J — Documentation and closure — complete at 5,479 tests.

Increment 46B physically established exact `:FUNCtion?` responses `CC`, `CV`,
`CR`, `CW`, and case-sensitive `SHORt`; exact `:INPut?` responses `OFF` and
`ON`; and invariant target responses with suffixes `V`, `A`, `OHM`, and `W`.
All queries were identity-gated and read-only. Front-panel-only mode and input
changes occurred with the external supply output off, followed by authoritative
restoration to CC and OFF, unchanged setpoints, normal session closure, and
independent port reopening.

Increment 46C established separate fixed `LOW?` and `UPP?` query paths and
physically reported ranges of 0.1000–120.00 V, 0.0000–30.000 A,
0.0500–7500.0 OHM, and 0.0000–300.00 W. A prior `:VOLTage? MIN` candidate
timed out without retry or mutation and was rejected. Final authoritative
queries confirmed CC, OFF, and unchanged targets before independent port
reopening.

Increment 46D physically established exact mode-selection commands and
readbacks: `:FUNCtion CC`/`CC`, `:FUNCtion CV`/`CV`, `:FUNCtion CR`/`CR`,
`:FUNCtion CW`/`CW`, and case-sensitive `:FUNCtion SHORt`/`SHORt`.
Legacy-shaped `:FUNCtion VOLT` and all-uppercase `:FUNCtion SHORT` candidates
were each rejected after one transmission and were not retried. Every successful
probe kept input OFF, transmitted one destination and one CC restoration
command, preserved all four targets, and closed in CC/OFF state. SHORT selection
did not activate the input and remains separate from explicitly confirmed SHORT
activation.

Increment 46E physically established invariant voltage, current, resistance,
and power setter forms with exact units. Each setter also selects its associated
mode: voltage selects CV, current selects CC, resistance selects CR, and power
selects CW. Same-value probes confirmed setter grammar and mode behavior.
Changed-value probes derived one bounded response-scale step, confirmed it,
restored the original target once, and restored CC once where required. Input
and the external supply output remained off, unrelated targets stayed
unchanged, and every successful run closed with all original targets in CC/OFF
state. No values were disclosed and no mutation was retried or replayed.

Increment 46F preserved immutable definition versions 1 and 2, added version 3
with eleven read-only identity, measurement, mode, input-state, and target
Properties, and added version 4 with the same inventory, four read/write target
Properties, and five parameterless mode-selection Commands. SHORT selection
does not activate the input. Input-control Commands, runtime mappings,
deployment, and migration remain excluded. Production continues to use
definition version 2 pending an explicitly approved offline migration.

Increment 46G carries version-3 reads and version-4 writes and mode Commands
through the serialized runtime, hosting, attachment, Runtime Host, and Client
paths. Installed version-2 profiles move to version 4 only through the explicit
offline, atomic, backed-up migration. Mutations retain input-OFF interlocks,
one transmission, authoritative readback, explicit uncertainty, and no retry or
recovery replay.

The Runtime Host exposes the five mode Commands through its ordered selector.
The final Client design uses direct CC, CV, CR, CW, and SHORT buttons rather
than a requested-selection dropdown. Each button maps to one exact published
Command. A press-only presentation guard prevents same-generation observation
reprojection from swallowing a click without hiding host, connection, or
generation changes.

Physical Client validation confirmed all five mode buttons with input OFF and
the external supply output OFF. Each mode change produced one Client Command
and matching authoritative displayed and physical readback. SHORT selection
did not activate the input, and the final state was restored to CC/OFF. The
validated automated baseline is 5,283 tests.

Increment 46H verifies version-4 supervised recovery after uncertain setpoint
and mode outcomes. Automated coverage requires one mutation transmission,
fault projection, preservation of the published endpoint and operation ports,
complete read-only resynchronization of all eleven Properties, and no mutation
replay on the replacement session.

Physical USB-disconnect validation changed the mode and one target at the
instrument while disconnected, then confirmed that the same endpoint and
attachment generation returned to Ready and adopted that authoritative state
without replaying cached intent. All post-recovery reads and explicit mode
Commands succeeded, diagnostics remained sanitized, and final state was
CC/OFF with the external supply output off. USB removal is detected by the next
Property or Command operation rather than by a passive idle health probe. The
validated automated baseline is 5,285 tests.

Increment 46I added immutable definition version 5 above version 4. Version 5
retains the eleven Properties, four writable targets, and five mode-selection
Commands, then adds parameterless `Input.Activate` and `Input.Deactivate` plus
`ShortCircuit.Activate` with one required Boolean confirmation argument. The
installed profile moved explicitly and offline from version 4 to version 5
through strict validation, atomic replacement, and a retained version-4 backup
without changing endpoint or serial-profile custody.

Ordinary activation authoritatively verifies input state and rejects SHORT.
Confirmed SHORT activation requires normalized Boolean `true` and verifies
authoritative SHORT/OFF state immediately before transmission. Deactivation is
available in every mode. Every accepted mutation is transmitted once, requires
authoritative readback, reports uncertainty without speculative cache changes,
and is never retried or replayed during recovery.

The Runtime Host and Client expose dedicated Activate input and Deactivate
input controls. The Client presents confirmed SHORT activation separately from
the five mode buttons. Its strict two-state confirmation survives only
connected same-host, same-generation observation refreshes and clears after
execution or any host, connection, or attachment-generation boundary.

Physical Host and Client validation confirmed activation, deactivation, and
separately confirmed SHORT activation with matching authoritative display and
instrument state. The external laboratory supply output remained OFF, each
accepted operation produced one Command execution, and the final state was
authoritative CC/OFF. The validation establishes the complete control path and
does not claim energized electrical-load performance. The validated automated
baseline is 5,479 tests.

Increment 46J reconciled ADR-0046, the characterization report, project status,
and this roadmap with the accepted version-5 implementation and physical
evidence. ADR-0046 is closed. Passive idle serial-health probing remains a
separate backlog item because the current attachment detects USB loss on the
next attempted Property or Command operation.

Current baseline:

```text
5,479 automated tests pass
ADR-0045 Runtime Host publication and recovery remain the production base
ADR-0046 controlled operating state, setpoints, and input control closed
Runtime Hosts and Client stopped
```

## Agreed later objectives

- Python Automation Boundary.
- Diagnostic Export and Offline Analysis.
- Remote Media Feedback.
