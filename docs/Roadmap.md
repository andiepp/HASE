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
- ADR-0046 — Controlled KEL-103 Operating State and Setpoints — complete at
  5,479 tests.
- ADR-0047 — Passive SCPI Instrument Health Supervision — complete at 5,497
  tests.
- ADR-0048 — SCPI Protocol and Bytes Diagnostics — complete at 5,533 tests.
- ADR-0049 — Authorized Remote Runtime Diagnostics — complete at 5,726 tests.
- ADR-0050 — Python Automation Boundary — complete.
- ADR-0051 — Python Client Local Distribution and Automation Workflows — complete.
- ADR-0052 — Python Client Examples and Laboratory Automation — complete at
  569 Python tests and 5,924 .NET tests after physical MiniPC Property,
  observation/Event, and Command validation.

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
evidence. ADR-0046 is closed. Its operation-detected idle-loss limitation was
subsequently resolved by ADR-0047 without changing ADR-0046 mutation,
uncertainty, or no-replay semantics.

Current baseline:

```text
5,533 automated tests pass
ADR-0045 Runtime Host publication and recovery remain the production base
ADR-0046 controlled operating state, setpoints, and input control closed
ADR-0047 passive SCPI health supervision closed
ADR-0048 SCPI Protocol and Bytes diagnostics closed
Runtime Hosts and Client stopped
```

## Completed objective — ADR-0047 Passive SCPI Instrument Health Supervision

**Status:** [Completed] Implemented, automated, physically validated, and closed
at 5,497 tests

ADR-0047 adds one fixed, characterized, read-only `*IDN?` health operation
through the existing published connection-slot and serialized SCPI-session
gates. The operation validates KEL-103 identity without changing Property
cache state or exposing identity content.

Each supervised attachment owns one monitor. It waits five seconds before the
first probe, probes only while Ready, and waits a complete interval after every
completed probe. It performs no catch-up and never accumulates or overlaps
probes. Failure projects sanitized Faulted state and delegates replacement and
complete authoritative read-only synchronization to existing recovery
supervision. No mutation is retried or replayed.

Completed increments:

1. 47A — Serialized KEL-103 health-probe primitive — complete at 5,491 tests.
2. 47B — Passive idle monitor lifecycle and physical validation — complete at
   5,497 tests.
3. 47C — ADR-0047 documentation and closure — complete at 5,497 tests.

Physical validation removed the KEL-103 USB connection without an operator
Property or Command operation. Host and Client left Ready, the same published
endpoint entered supervised recovery, and reconnection returned both to Ready
through authoritative synchronization. State remained CC/OFF, other endpoints
remained operational, diagnostics remained sanitized, and no state-changing
operation was retried or replayed.

Current baseline:

```text
5,497 automated tests pass
ADR-0047 passive SCPI instrument health supervision closed
KEL-103 passive USB-loss detection and authoritative recovery verified
Host and Client state remained truthful
No mutation retry or recovery replay occurred
```

## Completed objective — ADR-0048 SCPI Protocol and Bytes Diagnostics

**Status:** [Completed] Implemented, automated, physically validated, and closed
at 5,533 tests

ADR-0048 observes existing serialized SCPI exchanges without adding a second
transport path or instrument operation. Existing session construction remains
diagnostically inactive. Production KEL-103 sessions use one endpoint-scoped
observer, including sessions created during supervised recovery.

The established diagnostic disclosure levels apply:

- Operational capture emits no SCPI Protocol or Bytes records;
- Protocol capture emits correlated payload-free exchange metadata; and
- Bytes capture additionally emits exact snapshots bounded to 256 captured
  bytes with original length and truncation status.

Query and Command kind, transmit and receive direction, duration, sanitized
failure classification, and explicit uncertain Command outcome remain
available without changing timeout, framing, serialization, recovery, or
no-replay semantics. The `ScpiText` discriminator enables generic Runtime Host
structured presentation of printable ASCII body, Query/Command/response kind,
and the characterized CR request or LF response terminator.

Completed increments:

1. 48A — Transport-independent SCPI observation foundation — complete at
   5,508 tests.
2. 48B — Diagnostic disclosure and runtime record mapping — complete at 5,520
   tests.
3. 48C — Production KEL-103 composition and Host validation — complete at
   5,522 tests.
4. 48D — Structured SCPI text interpretation in the Runtime Host — complete at
   5,533 tests.
5. 48E — ADR-0048 documentation and closure — complete at 5,533 tests.

Physical validation used passive health and one authoritative measurement
Property read. Each exchange produced correlated Protocol and Bytes records
scoped to the KEL-103 endpoint. Transmitted bytes ended in `0D`, received bytes
ended in `0A`, Protocol details remained payload-free, structured presentation
agreed with raw capture, and the endpoint remained `Ready`. Validation required
no mutation and ended in authoritative CC/OFF state with the external
laboratory supply output OFF.

The Client Diagnostics window remains a truthful view of Client-side
northbound activity. It neither receives Runtime Host southbound snapshots nor
reconstructs them as Client-captured bytes. Authenticated remote Runtime Host
diagnostic projection requires a separate decision.

Current baseline:

```text
5,533 automated tests pass
ADR-0048 SCPI Protocol and Bytes diagnostics closed
Runtime Host raw and structured SCPI Bytes presentation verified
SCPI mutation uncertainty and no-replay behavior unchanged
KEL-103 final state CC/OFF; external laboratory supply output OFF
```

## Completed objective — ADR-0052 Python Client Examples and Laboratory Automation

**Status:** [Completed] Implemented, automated, physically validated, and closed at 569 Python / 5,924 .NET tests

ADR-0052 turns the supported installed Python Client boundary into practical repository-backed laboratory automation examples without adding a second framework. The completed examples cover explicit inventory inspection, one authoritative Property read, bounded repeated authoritative sampling, bounded live observation including Events, an explicitly confirmed same-value Property write with authoritative reconciliation, and an explicitly confirmed parameterless Command execution.

The examples preserve explicit Runtime Host selection through the external target registry, current attachment-generation targeting, deterministic channel ownership, no discovery, no default target, no fan-out, no failover, and no automatic reconnect. Mutations execute once, expose uncertain outcomes, and are never retried or replayed.

Physical MiniPC validation covered Arduino A0 measurement and sampling, physical button Events through live observation, a same-value `built-in-led-state` write, and one `Led/Toggle` Command with a visible one-time LED toggle. The accepted Laptop MiniPC Python principal now retains exactly snapshot read, authoritative Property read, observation subscription, Property write, and Command execution. Cached Property reads and diagnostics subscription remain absent.

Closure baseline:

```text
13 focused Command-example tests pass
569 complete Python tests pass
188 focused credential-provisioning tests pass
5,924 complete .NET Release tests pass
```

Credential lifecycle work is intentionally deferred to a separate architectural objective.

## Completed objective — ADR-0054 ESP32 Endpoint Library and Application Authoring Boundary

**Status:** [Complete] Implemented, physically validated, and closed at 6,024
passing complete .NET Release tests

Stable HASE ESP32 infrastructure is now packaged in the conventional
`libraries/HaseEsp32Endpoint` source-based Arduino library. The active
BME280/GPIO application contains five tracked source files plus local ignored
`HaseSecrets.h`, covering sketch composition, public endpoint configuration,
endpoint definition and registration, hardware application behavior, and
local Wi-Fi secrets.

The compatibility contract preserves Protocol Version 1.0, framed TCP port
5000, discovery and authoritative identity, descriptor bytes and ordering,
the three BME280 Properties, GPIO16 status-LED Property and Command, GPIO17
button Event, UTC timestamps, at-most-once mutations, live-only Events, Runtime
Host behavior, and recovery.

Stages:

1. 54A — complete: accepted decision and current-behavior compatibility
   contract;
2. 54B — complete: conventional library packaging and repeatable clean
   compilation without firmware upload;
3. 54C — complete: typed definition, callbacks, generic request processing,
   and active application boundary;
4. 54D — complete: migrated BME280/GPIO
   example, five tracked application files, external secrets template, and
   [ESP32 Endpoint Authoring Guide](ESP32-Endpoint-Authoring-Guide.md);
5. 54E — complete: read-only preflight, sensitive Current and Rollback bundle
   preparation, explicit single current-firmware upload, retained evidence,
   Runtime Host and Laptop Client validation, reconnect recovery, and direct
   Capability C-005 descriptor validation; and
6. 54F — complete: reconciled closure documentation and retained-custody record.

The pre-closure baseline is
`88e171590b6824546790c0e17c604f342865a4be`. Automated evidence records one
focused runtime-fixture build and two clean endpoint builds with zero warning
lines, a footprint of 1,010,594 flash bytes and 59,388 RAM bytes, and 6,024
passing complete .NET Release tests. AEPRAKETE, LABC, and LTAEP were clean and
synchronized before 54F.

Stage 54E invoked exactly one approved upload on COM6 without automatic retry
or rollback. Exact six-artifact Current and Rollback bundles remain in
sensitive local custody. Four generated `_flashed.bin` side effects remain in
separate local quarantine; cleanup and rollback remain separate explicit
operator actions. Runtime Host, Laptop Client, BME280 values, GPIO behavior and
Events, reconnect recovery, and the complete native Protocol Version 1
descriptor were physically validated. The native descriptor has no Compact
Serial numeric descriptor-version field.

Diagnostic Export and Offline Analysis remains accepted but is deferred.
ADR-0055 was selected next and is now completed below.

## Completed objective — ADR-0055 Runtime-Hosted Live Video and Audio

**Status:** [Completed] implemented, physically validated, documented, and
closed by Increment 55F5 at the 6,349-test baseline

ADR-0055 adds one explicitly selected view-only live camera from locally
configured sources on a Windows HASE Runtime Host and presents it in one remote
HASE WPF Client session. Optional microphone audio is associated locally with
that camera and requires its own authorization. Selection, Start, and Stop are
explicit Client actions.

The existing HASE gRPC/mTLS connection remains the control plane for sanitized
capability discovery, authorization, status, signaling, and session teardown.
Continuous video and audio use a separate, direct private-network WebRTC media
plane protected by DTLS-SRTP. Media is not represented as HASE Properties,
Events, diagnostics, ordinary gRPC payloads, or ESP32 Protocol Version 1
traffic.

The accepted initial implementation boundary embeds Microsoft Edge WebView2 in
the existing WPF Runtime Host and Client. WebView2 owns browser media capture,
WebRTC transport, decode, and rendering; C# owns HASE identity, authorization,
session policy, lifecycle, configuration, and sanitized diagnostics. The
[Runtime-Hosted Media Compatibility Contract](Runtime-Hosted-Media-Compatibility-Contract.md)
records the fixed 55A behavior and exclusions.

Read-only readiness succeeded on AEPRAKETE as Runtime Host and LTAEP as Viewing
Client. It confirmed the required Windows x64, .NET 10 Windows Desktop,
WebView2 Runtime, private-network, and privacy-policy prerequisites without
opening a camera or microphone, starting capture, installing dependencies,
running HASE applications, deploying software, or changing physical state.

Stages:

1. 55A — complete: architecture decision, technology discovery, compatibility
   contract, and read-only host/client readiness;
2. 55B — complete: media capability model, exact authorization actions,
   versioned protobuf control service, fixed limits, compatibility tests, and
   6,061-test complete Release validation;
3. 55C — implemented and automatically validated: Windows Runtime Host camera
   and microphone capture boundary, exact device/session ownership, hardened
   local WebView2 origin, and 6,113-test complete Release validation; committed
   as `654ce26560d4e7688984a31bd515a2590ca2448d`;
4. 55D — revised implementation automatically validated: multiple
   operator-configured logical Runtime Host cameras, remote sanitized camera
   selection, WPF Client Start/Stop and audio controls, and receiver-only
   WebView2 presentation; committed as
   `199356222f8763ed6e6fbb5f481fe46aa70ec679`;
5. 55E1 — implemented and automatically validated: authenticated
   transport-neutral
   media-control service, Client gRPC adapter, duplex offer/answer/ICE exchange,
   sequencing, acknowledgments, bounds, lease renewal, and 6,158-test complete
   Release validation; committed as
   `c7d509f43a34948695656614ba5131fb526a4450`;
6. 55E2 — implemented and automatically validated: Runtime Host offerer and
   Client answerer WebView2 peer boundaries with mandatory DTLS-SRTP; complete
   Release validation passes 6,177 tests and the implementation is committed as
   `1c115e0e4f12b0b10d2cddbca9b90f50bf70523f`;
7. 55E3 — implemented and automatically validated: explicit local
   configuration, conditional service and application composition, packaging
   custody, failure recovery, and 6,202-test complete Release validation; and
8. 55F1 — complete: read-only installed Runtime Host and Client state,
   certificate, enrollment, authorization, process, and WebView2 application
   readiness discovery without mutation;
9. 55F2 — implemented for automated validation: explicit local binding mode,
   protected preparation artifacts, exact Client credential correlation,
   least-privilege media authorization, transactional enablement, verification,
   and retained rollback tooling without execution; and
10. 55F3 — controlled application update, protected binding and authorization
   preparation, transactional enablement, and one-camera end-to-end live-video
   validation complete. Explicit Start displayed live video on LTAEP; Stop
   released capture; Runtime Host and three existing endpoints remained
   running and the Client remained connected.
11. 55F4 — complete: protected multi-camera preparation; transactional
   two-camera and optional-audio rebind; durable WebView2 custody; available-
   endpoint-only Runtime Host startup; deterministic first-attempt Client
   presentation; explicit audio activation; and split video/audio rendering.
   Controlled validation confirms both cameras, switching, clean stop,
   optional microphone playback, and unavailable/restored endpoint behavior.
   The final implementation commit is
   `0c8b113cd05641ccd478d4bed017ef0c12a5f92c`, with 6,349 passing tests.
12. 55F5 — complete: ADR, Project Status, and Roadmap reconciliation; retained
   protected recovery evidence; Arduino Uno Compact endpoint authoring
   guidance; and closure.

The [Arduino Uno Compact Endpoint How-To](Arduino-Uno-Compact-Endpoint-How-To.md)
records the supported Boolean and millivolt Property mappings, parameterless
Commands, null-payload Events, descriptor registration, Runtime Host endpoint
composition, and the C# discovery change required before multiple identical
VID/PID boards can be attached deterministically.

The initial objective excludes recording, snapshots, PTZ, public-internet
relay, STUN/TURN service deployment, multiple simultaneous viewers, remotely
managed or automatically selected Runtime Host sources, ESP32 media, and
physical deployment.

Increment 55C starts from exact commit
`8f5a594053debb53aae120ba72edac415a7a2976` and is committed as
`654ce26560d4e7688984a31bd515a2590ca2448d`. It adds a new dependency-light
media-domain project and tests, pins the WebView2 SDK in the Windows Desktop
Host application, and adds repository-owned HTML, JavaScript, CSS, browser
policy, bridge validation, and a non-composed capture adapter. The session
owner enforces one exact source and principal, one active session, ordered and
bounded negotiation, lease and negotiation timeouts, explicit stop, and
exactly-once cleanup. The adapter is not registered at startup and remote
negotiation is rejected until the separately approved end-to-end stage.

55C focused validation and the complete Release suite succeeded on AEPRAKETE:
6,113 tests passed with zero failures and zero skips, and the successful build
reported 56 warnings. Repository application and automated validation did not
initialize WebView2, open a camera or microphone, start HASE, exchange
signaling, publish configuration, deploy software, change firewall or privacy
policy, or perform physical work.

The approved 55D architecture amendment supports multiple cameras configured
locally on a Runtime Host. Each source has an operator-defined sanitized ID and
display name, a current generation, an exact host-local Windows device binding,
and optional associated microphone. Capability discovery exposes only the
logical fields. The Client explicitly selects one exact generation before
Start; switching requires Stop and a fresh Start, and unavailable or stale
sources never fall back to another camera. One session remains active
application-wide.

The 55D source adds multi-source session ownership and capability projection,
the additive sanitized `display_name` contract field, Client-side media models
and control seam, logical camera selector, optional-audio and Start/Stop
controls, a reserved presentation surface, and repository-owned receiver-only
WebView2 assets and browser policy. The boundary remains uncomposed until 55E3;
it requests no Client camera or microphone permission and receives no transport
or media during 55D repository application or automated validation.

The focused media contract, multi-source session-owner, capability-projection,
Client selection, and Client WebView2 policy suites succeeded on AEPRAKETE.
The complete Release suite passes 6,139 tests with zero failures and zero
skips; the successful build reports 45 warnings. Validation did not start an
application, initialize WebView2, enumerate or access a media device, capture
media, exchange signaling, deploy, or change firewall, privacy, credential,
firmware, or physical state.

The exact 27-path 55D implementation is committed as
`199356222f8763ed6e6fbb5f481fe46aa70ec679`; its final documentation
reconciliation and synchronized 55E1 baseline is
`7b4ffe78920aeaa2e356b5d7a3e84b43ca493dc4`.

Approved Increment 55E1 adds the concrete generated gRPC service adapter and
Client media-control adapter without registering either in application
startup. The process-local owner now maintains independent Host delivery and
Client submission sequence spaces. The Runtime Host may publish exactly one
initial offer plus bounded ICE messages; the Client may submit exactly one
answer plus bounded ICE messages. Acknowledgments remove only delivered Host
messages, empty exchanges renew the lease, and invalid ownership, ordering,
role, count, size, timeout, or authorization fails closed with sanitized
status.

55E1 validation used fakes and direct service/client calls. Focused validation
and the complete Release suite succeeded on AEPRAKETE with 6,158 passed, zero
failed, and zero skipped. It did not start
an application, initialize WebView2, enumerate or access a device, capture
media, create a browser peer connection, exchange live network signaling,
register a gRPC endpoint, publish configuration, deploy, or change firewall,
privacy, credential, firmware, or physical state. The exact implementation is
committed as `c7d509f43a34948695656614ba5131fb526a4450`.

Approved Increment 55E2 extends the repository-owned Runtime Host capture and
Client presentation scripts with `RTCPeerConnection` boundaries. The Host is
the only offerer and uses send-only transceivers; the Client is the only
answerer and forces receive-only transceivers without `getUserMedia`. Both use
an empty ICE-server list, no data channel, mandatory RTCP mux and SHA-256 DTLS
fingerprints, VP8 video, and optional Opus audio. Local candidates are queued
until the offer or answer is published so the 55E1 role and sequence contract
remains authoritative.

55E2 also extends both narrow web/native validators and adapters for bounded
offer, answer, ICE, peer-connected, and sanitized-failure messages. It remains
uncomposed until 55E3. Applying and testing the source does not initialize
WebView2, create a live peer, enumerate or access a media device, capture or
render media, exchange network signaling, register a service, deploy, or
change firewall, privacy, credential, firmware, or physical state.

Approved Increment 55E3 keeps media disabled when the Runtime Host application
profile omits an external media-configuration path. When configured, a strict
version 1 file binds sanitized logical source identity and generation to exact
local Windows camera and optional microphone device identities. The file is
separate from endpoint composition, requires an explicit authorization policy,
and is preserved by application-only updates. No device is enumerated while
configuration is loaded.

The existing private-network host registers the generated media-control
service only for configured media and uses the existing authenticated principal
and exact policy. The Runtime Host capture and Client presentation WebViews are
constructed by their WPF shells but initialize only after explicit Start. The
Client reuses its selected connected profile, serializes bounded negotiation,
renews the lease after the peer connects, and clears local presentation on
Stop, selection change, disconnect, failure, or shutdown. Existing reconnect
never replays Start or resumes a media session.

Focused 55E3 validation and the complete Release suite succeeded on
AEPRAKETE. The complete suite passes 6,202 tests with zero failures and zero
skips; the successful build reports 39 warnings. The exact 54-path source
scope was explicitly accepted for commit. Validation did not start
either application, initialize WebView2, deploy, enumerate or access a media
device, capture media, create a live peer, exchange live signaling, alter
authorization or credentials, or mutate physical state.

## Completed objective — ADR-0053 Python Credential Lifecycle and Recovery

**Status:** [Complete] Implemented, physically validated, and closed

ADR-0053 extends the dedicated Python mutual-TLS provisioning boundary beyond
initial enrollment. It adds explicit expiry classification, planned rotation,
replacement, revocation, loss and corruption recovery, emergency compromise
replacement, installed-environment cutover, protected evidence, and independent
Desktop/MiniPC transitions.

Initial provisioning remains unchanged and continues to reject an
already-authorized principal. Rotation is a separate lifecycle transaction:
the new credential is temporarily enrolled for the same principal and trust
policy, authorization remains exact and byte-stable, the installed Client
selects and validates the replacement through a fresh session, and only then is
the old enrollment removed and obsolete private-key custody destroyed.

Stage 53A establishes the accepted decision and an offline, read-only inspector
that proves certificate/key identity, exact enrollment, principal and trust,
exact expected grants, trusted-server custody, UTC validity state, and source
revisions before a later lifecycle publication is permitted.

Stage 53A closed at 204 focused credential-provisioning and 5,940 complete .NET
tests. Stage 53B begins with a separate in-memory rotation preparer. It
re-inspects and revision-locks the complete selected deployment, validates a
new and distinct certificate/private-key pair, creates an overlap enrollment
containing exactly the old and new credential for the same principal and trust
policy, creates the final enrollment with only the replacement, and carries
the profile and authorization policy byte-exact. It publishes no file and
therefore cannot create a partial cross-computer transition.

The durable Stage 53B publisher begins only from those locked candidates. It
records a bounded metadata-only journal before staging, preserves exact hashes
and access control for all four replaced files, publishes the overlap
enrollment last, and deliberately retains the originals and final-enrollment
candidate across external Client transfer and validation. Explicit rollback
restores every exact source. Explicit finalization requires the complete
candidate state and unchanged authorization policy, replaces only the overlap
enrollment with the validated final enrollment, proves the old identity absent
and the replacement present, and then removes obsolete backups and transaction
artifacts.

The Stage 53B orchestration boundary composes strict reinspection, candidate
preparation, and durable `Begin` without hiding finalization or recovery.
Automated interruption injection covers the staged boundary, each of the four
published files, and completed overlap publication. Every injected failure is
required to restore the exact certificate, key, profile, enrollment, and
policy hashes and remove all transaction artifacts.

Stage 53B closed at 216 focused credential-provisioning and 5,952 complete .NET
tests. Stage 53C exposes the composed lifecycle through the existing Windows
operator. `rotate-begin` requires the exact current credential ID, certificate,
key, profile, enrollment, policy and trusted-server hashes, principal, trust
policy, grant set, signing root, and bounded validity. `rotate-finalize` and
`rotate-recover` accept only the exact retained publication inputs. Output
contains fixed outcomes and transaction metadata while withholding every
deployment value.

Physical Stage 53C2A preflight found and corrected the colocated-custody
assumption for Laptop-to-MiniPC rotation. The old Laptop private key never
returns to the MiniPC. A protected metadata request binds its identity and
revisions; the MiniPC publishes overlap enrollment and emits only newly issued
replacement custody for explicit Laptop cutover.

## Agreed later objectives

- Diagnostic Export and Offline Analysis.

## Completed objective — ADR-0049 Authorized Remote Runtime Diagnostics

**Status:** [Completed] Implemented, automated, physically validated, and closed
at 5,726 tests

ADR-0049 adds an explicitly authorized, bounded, live-only projection of
Runtime Host diagnostics into the existing multi-host Client diagnostics
window. Local and remote disclosure ceilings both apply. Every subscription
requires `diagnostics.subscribe`; denial is sanitized and independent of
inventory, Property, Command, Event, and endpoint recovery operation.

The gRPC adapter strictly validates projected records and stream sequence. Each
profile owns an independent subscription and bounded recovery schedule. Fresh
subscriptions replay nothing, and diagnostic recovery never retries or replays
a mutation. Projected records retain Host timestamp, profile and endpoint
scope, correlation, original and captured byte counts, truncation, and exact
bounded hexadecimal content.

Physical validation confirmed authorized Operational, Protocol, and Bytes
delivery for passive KEL-103 health and one authoritative voltage read. The
request ended in `0D`; a fragmented response retained exact chunks and its final
correlated chunk ended in `0A`. Reconnect produced no replay or duplicate.
Removing only the diagnostic grant caused sanitized denial without affecting
inventory or authoritative reads. Exact policy restoration and pre-migration
profile restoration returned remote diagnostics to disabled while ordinary
operation remained available.

Current baseline:

```text
5,726 automated tests pass
ADR-0049 Authorized Remote Runtime Diagnostics closed
Remote diagnostics disabled after supervised profile restoration
KEL-103 final state CC/OFF; external laboratory supply output OFF
```

## Completed objective — ADR-0050 Python Automation Boundary

**Status:** [Completed] Implemented, automated, physically validated, and closed
at 325 Python tests, 159 focused credential-provisioning tests, and 5,895 .NET
tests

ADR-0050 exposes all seven version-1 Runtime Host RPCs through the external
asyncio-native `hase-client` package while preserving mutual TLS, explicit
authorization, generation-qualified targets, mutation uncertainty, no retry or
replay, and live-only streaming semantics. Physical validation covered
snapshot, cached and authoritative reads, same-value Property write, one CC
Command, observation, and authorized diagnostics. Exact security restoration
removed the diagnostic grant and disabled remote diagnostics.

## Completed objective — ADR-0051 Python Client Local Distribution and Automation Workflows

**Status:** [Completed] Locally distributed, installed, physically validated on
Desktop, MiniPC, and Laptop, and closed at 494 Python tests, 161 focused
credential-provisioning tests, and 5,897 .NET tests

ADR-0051 adds versioned local wheel production, content and SHA-256 records,
fresh installed-package validation, private persistent automation environments,
guarded KEL-103 workflows, a dedicated MiniPC Python identity, and a dedicated
Laptop-to-MiniPC read-only identity. The Laptop installed `hase-client 0.6.0`
and selected exactly two external targets without discovery, fan-out, failover,
retry, or redirection.

Physical closure validated installed Laptop Health workflows against both the
Desktop and MiniPC Runtime Hosts and one authoritative MiniPC Arduino A0 read.
All protected Laptop custody remained unchanged. Temporary wheel-transfer and
duplicate MiniPC private-key staging custody were removed; rollback and
preparation evidence remain protected. Runtime Hosts and Clients were stopped,
the KEL-103 remained CC/OFF, and the laboratory supply output remained OFF.

Stage 53C2A3 adds protected Laptop import, rollback-capable replacement of the
installed certificate, private key and profile, and independent byte-exact
verification. After automated validation, commit, push, and three-computer
synchronization, physical validation must prove a fresh Python session with the
replacement credential. Only then may a separate explicit MiniPC finalization
remove the old enrollment and obsolete private-key custody.

Stage 53C2A3A corrects Windows Access-only security-descriptor capture after a
fully recovered physical cutover attempt. Automated validation, commit, push,
and three-computer synchronization remain mandatory before retrying the Laptop
cutover in fresh protected custody.

Stage 53C2A3B replaces reconstructed Windows ACL descriptors with direct
original `FileSecurity` object preservation. Automated validation, commit,
push, and three-computer synchronization remain mandatory before a fresh
Laptop cutover retry; both earlier failed transactions remain protected
recovery evidence.

Stage 53C2A3C removes privileged installed-file ACL writes and requires
before/after Access-SDDL equality around content-only replacement and rollback.
Automated validation, commit, push, and three-computer synchronization remain
mandatory before a fresh Retry3 cutover; all three failed attempts remain
protected recovery evidence.

Stage 53C2A3D removes the redundant post-update journal ACL application.
Automated validation, commit, push, and three-computer synchronization remain
mandatory before a fresh Retry4 cutover. Failed4 evidence deliberately records
`replacement-installed` while independently verified installed content is old.

### ADR-0053 Increment 53C2A4 — MiniPC finalization boundary

- Implement the explicit MiniPC transaction that consumes the physically proven
  replacement-connection decision and revokes only the old enrollment.
- Retain protected Begin, overlap, original, archive, and committed-finalization
  evidence while keeping authorization and trust byte-exact.
- Recover only an interrupted prepared finalization; never silently reintroduce
  an old credential after commit.
- After repository validation and synchronization, finalize physically with both
  applications stopped, independently validate, then prove one fresh
  replacement-only mutual-TLS connection.

53C2A4A corrects the pre-execution Windows custody boundary: inherit private
access from the protected Begin directory, verify effective current-user-only
rules, and perform no redundant ACL write. Repeat repository validation and
synchronization before physical finalization.

53C2A4B corrects only Windows owner representation in the private-custody
verifier by translating `Get-Acl.Owner` to SID. Repeat automated validation and
three-computer synchronization before physical finalization.

Stage 53C2A4 is complete. The replacement-only MiniPC enrollment is durable,
the old enrollment is revoked, authorization and ACLs are unchanged, protected
rollback evidence is retained, and a fresh post-finalization Laptop Python TLS
channel succeeded. Both applications are stopped and the three repositories
remain synchronized. Subsequent lifecycle work may address separately approved
retention expiry and obsolete private-key cleanup; it must not weaken the
accepted replacement-only state.

### ADR-0053 Increment 53C2A5 — obsolete private-key cleanup

- Discover and supply an explicit set of transaction-bound failed/accepted
  Laptop cutover custody directories.
- Require finalization and replacement-only connection proof before cleanup.
- Match every obsolete rollback key to the authoritative old-key hash, protect
  the active replacement key, quarantine first, and durably commit deletion.
- Retain non-secret evidence and resume interrupted cleanup deterministically.
- Validate, commit, push, and synchronize all computers before physical cleanup.

Stage 53C2A5 is complete. All five transaction-bound obsolete old private-key
rollback copies are absent; the active replacement is unchanged; fifteen
non-secret journals, certificates, and profiles remain exact; and a committed
cleanup journal is durable. Both applications are stopped. Subsequent work must
preserve the replacement-only enrollment. Any separately approved
evidence-retention expiry remains outside this implementation stage.

## ADR-0053 closure

ADR-0053 is complete at 238 focused credential-provisioning tests and 5,974
complete .NET tests. The final accepted state has one active replacement
credential, no old MiniPC enrollment, no obsolete old-key rollback copies, an
unchanged principal and exact five-grant authorization set, durable protected
transaction evidence, and a proven replacement-only mutual-TLS channel.
AEPRAKETE, LABC, and LTAEP are clean and synchronized, and both applications
are stopped. Any future credential-lifecycle extension requires a new approved
architectural objective; retained non-secret evidence expiry remains a separate
operator-approved policy decision.

---

## Completed objective — ADR-0056 Dynamic Runtime-Host Camera Inventory

**Status:** [Complete] Implemented, deployed, physically validated, and closed
at 6,362 passing tests

ADR-0056 extends the closed ADR-0055 media boundary so a Windows Runtime Host
can reconcile cameras that are plugged in or disconnected while the
application remains running. Device observation remains local to the Runtime
Host. The Client receives only sanitized logical identities, current
generations, display names, availability, and media capability ceilings.

The completed design adds startup inventory discovery, bounded device-change
reconciliation, a protected local logical-identity registry, and an
authenticated change-driven capability stream. A disconnected active camera
ends its session with `SourceLost`; a returning camera receives a new
generation. The Client updates its available list and may preselect the sole
remaining current idle camera, but never starts, resumes, switches an active
session, or falls back automatically.

Existing one-session, one-viewer, mTLS authorization, WebRTC media, explicit
Start/Stop, audio opt-in, no-recording, no-public-relay, and no-device-identifier
disclosure contracts remain unchanged. Dynamic microphone discovery, PTZ,
recording, snapshots, multiple viewers, public relay, and non-Windows capture
remain outside ADR-0056.

Increment 56B implemented the source boundary. Increment 56C migrated the two
AEPRAKETE bindings into protected format-2 custody and deployed the Runtime
Host and Client. Increment 56C1 corrected active-loss Client reconciliation and
is accepted at commit `58a0c4e29298b5605758b4c837061d295af7a483` with
261 focused and 6,362 complete passing tests.

Physical validation confirmed initial inventory, idle disconnect/reconnect,
stable logical names, explicit Camera 2 video, active source-loss stop,
automatic stale-entry removal, sole Camera 1 preselection without automatic
Start, and restored two-camera inventory after reconnect without errors.
Protected migration recovery evidence remains retained; closure does not
authorize its deletion.
Diagnostic Export and Offline Analysis remains accepted but deferred.

---

## Completed objective — ADR-0057 Client Workspace and Detached Media Presentation

**Status:** [Completed] Implemented, validated, synchronized, and closed at
6,369 passing complete Release tests

Completed:

- three-column Runtime Host / Endpoint / Properties-and-Commands Client
  workspace;
- Runtime Host connect/disconnect tiles with green connected state;
- stable endpoint selection across projection refresh;
- stable Boolean requested Property edits across same-host, same-attachment
  refresh;
- detached `Video / Audio` window using the existing media control and WebView2
  presentation boundaries;
- LTAEP application-only publication with configuration and shortcut custody
  preserved;
- 268/268 focused Client tests and accepted shortcut-launched Client behavior;
- AEPRAKETE, LABC, and LTAEP repository synchronization; and
- final AEPRAKETE complete Release validation of 6,369/6,369 tests.

Implementation commits are
`801f3fb834472d30331b937778fd6fe8f9dea8b1` and
`71924a773a60473eb21028967b4002920996b7eb`; the documentation/synchronization
checkpoint is `496b3a316c9ab92fb80f37235efa800c0675893b`.

Diagnostic Export and Offline Analysis remains accepted but deferred.

---

## Completed objective — ADR-0058 Operator-Initiated Runtime Endpoint Refresh

**Status:** [Completed] Implemented, validated, synchronized, deployed,
physically verified, and closed at 6,379 passing complete Release tests

ADR-0058 adds an explicit `Refresh` action adjacent to `Open Diagnostics` in
the Windows Runtime Host. The action searches only for configured physical
endpoints that are not currently published, authoritatively verifies their
identity, and attaches an exact configured endpoint without restarting the
Runtime Host.

The operator action is distinct from the existing one-second inventory
presentation refresh. It is serialized, disabled while active,
shutdown-cancellable, and failure-isolating. Completion immediately reprojects
the authoritative endpoint inventory and diagnostics.

Existing published endpoints remain untouched in every connection state and
continue to own their established disconnect/reconnect supervision. Refresh
does not replace an attachment, change an existing generation, replay an
operation or Event, or start or alter media.

The completed implementation covers configured native-network, Compact Serial,
and KEL-103 serial endpoints. In-process simulation, arbitrary unconfigured
candidates, continuous hot-plug monitoring, runtime composition editing,
automatic attachment, and automatic endpoint replacement remain excluded.

ADR-0058 begins at exact commit
`f7615ae79e72efc48935eed63e02ff650d2d0a87` with 6,369 complete Release tests.
Increment 58A is commit `039f28aad45dde425e8b887bc41e5c6d41d458dc`.
The exact 13-path Increment 58B implementation is commit
`fa491eeb821bcf0252ff71542d89605377187ed8`; focused validation and the complete
Release suite succeeded on AEPRAKETE with 6,379 passed, zero failed, and zero
skipped. AEPRAKETE, LABC, and LTAEP are clean and synchronized at that commit.

The AEPRAKETE application-only deployment preserved configuration, identity,
authorization, shortcut, and WebView2 custody. Controlled physical validation
proved startup containment with `arduino-uno-01` unavailable, successful exact
attachment after connecting it and pressing Refresh, three `Ready` endpoints,
unchanged pre-existing generations, and duplicate prevention on a repeated
Refresh. The operator accepted the complete scenario as working perfectly.

The verified pre-deployment application rollback and SHA-256 manifest remain in
local AEPRAKETE custody. ADR-0058 closure does not authorize their deletion.

## Completed objective — ADR-0059 Client Connection Controls and Pinned Media Sessions

ADR-0059 records the completed laptop-client interaction and reliability
corrections as one retrospective architectural objective: an explicit
per-entry `Connect`/`Disconnect` control in the runtime-host selection list, a
retained detached media window that survives closing and reopening, clean
client shutdown in every close order including during an active stream,
repeatable reconnection of a runtime host that faulted after a successful
connection, and media sessions that pin their runtime host so a running stream
survives inventory selection changes.

ADR-0059 begins at exact commit
`5205972bcc307b6a5c4d36ab95121bdccf5676c4` with 6,379 complete Release tests
and closes at commit `90cc3cd77724124d2b193c82d06f2d2bc50405cd` with 6,391
passed, zero failed, and zero skipped. Every increment was deployed to the
installed LTAEP client with `Update-HaseClient.ps1`, preserving Runtime Host
registry and desktop shortcut custody, and was physically verified by the
operator.

Diagnostic Export and Offline Analysis remains accepted but deferred.
