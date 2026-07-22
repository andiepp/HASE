# HASE Roadmap

## Vision

HASE is built in layers. Each completed phase becomes a stable foundation for the following phases, and architecture changes should become increasingly rare as the framework matures.

HASE provides transport-independent access to physical and simulated hardware instruments through a common descriptor, runtime, protocol, and tooling model.

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

Key outcome: HASE established a transport-independent representation of endpoints and instruments.

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

Future extensions include noise, calibration, playback, JSON scenarios, and fault injection.

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

Key outcome: a deterministic binary protocol foundation independent of transport.

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

Protocol Version 1 messages are `DiscoverRequest`, `DiscoverResponse`, `ReadEndpointDescriptorRequest`, `ReadEndpointDescriptorResponse`, `ReadPropertyRequest`, `ReadPropertyResponse`, `WritePropertyRequest`, `WritePropertyResponse`, `ExecuteCommandRequest`, `ExecuteCommandResponse`, and `EventNotification`.

Protocol Version 1 is feature complete for the current Properties, Commands, and Events contract.

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

**Status:** [Active] Active - major capabilities completed

Current baseline:

```text
1,677 automated tests
.NET solution builds
ESP32 firmware builds
Arduino Uno firmware builds
Physical ESP32 endpoint verified
Physical Arduino Uno endpoint verified
IPv4 network discovery verified
Windows USB serial discovery verified
Compact serial endpoint attachment verified
```

## 6.1 Transport Abstraction

**Status:** [Completed] Completed

Implemented `Hase.Transport`, transport connection and factory contracts, duplex connections, lifecycle states, loopback migration, and contract tests.

## 6.2 Framed TCP Transport

**Status:** [Completed] Completed

Implemented TCP options, connection factory, four-byte big-endian framing, payload validation, connection timeouts, duplex send/receive, invalidation, tracing, concurrency tests, and failure tests.

## 6.3 Runtime Transport Integration

**Status:** [Completed] Completed

Implemented connection management, legacy and duplex protocol connections, protocol sessions and bindings, endpoint synchronization, and connection coordination.

## 6.4 Automatic Reconnection

**Status:** [Completed] Completed

Implemented initial retry, transport replacement, bounded backoff, complete resynchronization, cached-value preservation, cancellation-aware supervision, and diagnostics.

```text
immediate
1 second
2 seconds
5 seconds
10 seconds maximum
```

## 6.5 Duplex Protocol Health Probing

**Status:** [Completed] Completed

Implemented coordinator-owned probing, explicit timeouts, silent-loss detection, transport invalidation, recovery through the existing supervisor, one receive path, and physical ESP32 reset validation.

Architecture: ADR-0017.

## 6.6 Runtime Event Routing and Recovery

**Status:** [Completed] Completed

Implemented unsolicited notification routing, observer subscriptions, runtime event routing, router migration, physical GPIO17 notification, post-recovery validation, and logical diagnostics across sessions.

## 6.7 Physical ESP32 Endpoint

**Status:** [Completed] Completed for the current endpoint contract

Hardware includes the DOIT ESP32 DEVKITC V4 / ESP32-WROOM, BME280, GPIO controller, Wi-Fi, and framed TCP port 5000.

Physical discovery, descriptor access, property reads and writes, commands, events, supervision, reconnect, probing, resynchronization, and notification recovery are verified. Capabilities C-003 through C-014 are complete.

## 6.8 Network Endpoint Discovery

**Status:** [Completed] Implemented and physically verified for IPv4

```text
Technology : mDNS/DNS-SD
Service    : _hase._tcp.local
Instance   : doit-esp32-devkitc-v4-01
TCP port   : 5000
```

Implemented:

- `NetworkEndpointCandidate` and `INetworkEndpointBrowser`;
- `MdnsNetworkEndpointBrowser` and isolated `Tmds.MDns` adapter;
- cancellation-aware browsing and IPv4 filtering;
- candidate deduplication by address and port;
- candidate verification contracts;
- framed-TCP Protocol Version 1 verifier;
- timeout, unreachable, non-HASE, and invalid-response isolation;
- authoritative `EndpointId` extraction;
- verified endpoint deduplication by `EndpointId`;
- discovery orchestration;
- Protocol Explorer `network-discovery` scenario;
- ESP32 mDNS advertiser coordinated with network startup;
- clean Ctrl+C cancellation.

Physical result:

```text
Service  : doit-esp32-devkitc-v4-01
Candidate: 192.168.0.223:5000
Result   : Verified
Endpoint : doit-esp32-devkitc-v4-01
```

Constraints:

- mDNS advertises reachability, not identity;
- `DiscoverResponse.EndpointId` is authoritative;
- Protocol Version 1 remains unchanged;
- candidate failures remain isolated;
- cancellation stops browsing and verification;
- discovered endpoints never replace runtime endpoints automatically;
- one discovery session produces a unique endpoint inventory;
- same-identity endpoint reappearance is not emitted again during that session;
- live Added/Updated/Removed presence tracking remains backlog;
- authentication and authorization remain out of scope.

Physical capability C-015 covers IPv4 mDNS/DNS-SD discovery and Protocol Version 1 verification.

A physical reset test confirmed that an endpoint returning with the same address, port, and authoritative identity is not emitted as a duplicate result. This is the intended unique-inventory behavior.

Architecture: ADR-0018 - mDNS/DNS-SD Network Endpoint Discovery.

## 6.9 Explicit Endpoint Attachment and Lifecycle Ownership

**Status:** [Completed] Implemented and physically verified for native framed TCP

Architecture: ADR-0019 - Local Endpoint Communication Lifecycle Ownership.

The HASE runtime host owns the complete local communication lifecycle for every attached endpoint:

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

Approved boundaries:

- discovery and manual configuration are equal connection-definition sources;
- detection never attaches or replaces a runtime endpoint automatically;
- attachment requires an explicit application or user request;
- native HASE identity is verified again on the operational connection;
- network addresses are not authoritative endpoint identity;
- descriptor resolution is independent of connection resolution;
- complete descriptors, compact repository references, and adapter-configured descriptors are supported architecture paths;
- an attachment session owns the runtime endpoint and its complete communication lifecycle;
- one endpoint connection may operate multiple instruments;
- multiple applications access physical endpoints through the runtime host;
- Tailscale host detection and a future northbound API remain above the local lifecycle;
- Protocol Version 1 remains unchanged.

Implemented:

- endpoint connection-origin and connection-definition contracts;
- endpoint-provided descriptor-source and attachment contracts;
- native Protocol Version 1 bootstrap contracts and implementation;
- temporary framed-TCP bootstrap client;
- staged runtime endpoint creation and explicit publication;
- operational identity validation on the connection entering service;
- initial-readiness gating before publication;
- coherent operational resource construction;
- runtime-host-owned attachment sessions with idempotent orderly shutdown;
- failed-attachment cleanup and caller-cancellation propagation;
- manual and discovery-derived network definitions converging on one attachment service;
- automated framed-TCP bootstrap, operational attachment, publication, and shutdown integration;
- Protocol Explorer C-016 physical validation.

Physical validation confirmed:

```text
Authoritative endpoint : doit-esp32-devkitc-v4-01
Connection state       : Ready
Published endpoints     : 1
Readable cache          : Temperature, Relative Humidity,
                          Air Pressure, Status LED Enabled
Shutdown state          : Disconnected
Published endpoints     : 0
```

The ESP32 transport now accepts a newly pending operational client and replaces the stale bootstrap client when the network stack has not yet observed peer closure.

Capability C-016 is complete for native framed-TCP endpoints.

## 6.10 Runtime-host Attachment Inventory

**Status:** [Completed] Implemented and physically verified for native framed TCP

Capability C-017 adds the host-owned inventory above the C-016 attachment lifecycle.

Implemented:

- `IRuntimeEndpointAttachmentInventory`;
- immutable `RuntimeEndpointAttachmentInventoryEntry`;
- authoritative identity from the attached `RuntimeEndpoint`;
- attach, find, snapshot list, detach, and asynchronous disposal;
- duplicate authoritative-identity rejection without automatic replacement;
- cleanup of rejected duplicate sessions;
- explicit failure propagation and disposal-failure aggregation;
- deterministic coordination of attachment, detachment, lookup, listing, and disposal;
- `RuntimeEndpointAttachmentHost` ownership composition;
- native framed-TCP host factory;
- automated framed-TCP host-inventory integration;
- Protocol Explorer C-017 physical validation.

Concurrency rules:

- complete inventory operations are serialized;
- an attachment already in progress completes before queued disposal;
- disposal owns and closes attachments completed before it;
- operations queued behind disposal are rejected;
- duplicate identity never replaces the existing attachment.

Physical validation confirmed:

```text
Authoritative endpoint : doit-esp32-devkitc-v4-01
Connection state       : Ready
Inventory entries      : 1
Authoritative lookup   : Same entry
Published endpoints    : 1
Detached               : True
Shutdown state         : Disconnected
Inventory entries      : 0
Published endpoints    : 0
```

Capability C-017 is complete for the current native framed-TCP endpoint path.

## 6.11 Resource-Constrained USB Serial Endpoints

**Status:** [Completed] Implemented and physically verified for Arduino Uno-class endpoints

Architecture: ADR-0020 - Resource-Constrained Serial Endpoint Protocol.

Implemented:

- production USB serial byte transport;
- Compact Serial Protocol Version 1 framing and CRC validation;
- authoritative compact bootstrap identity;
- versioned host-side descriptor repository resolution;
- compact command execution;
- descriptor-side compact property mappings;
- descriptor-selected Boolean value decoding;
- compact property reading;
- compact runtime property-cache synchronization;
- cache preservation after unsuccessful compact reads;
- Arduino Uno bootstrap firmware and Protocol Explorer C-018;
- physical LED-toggle command validation through C-019;
- physical `Led.State` cache synchronization through C-020;
- compact endpoint connection ownership and coordinated replacement;
- recurring compact protocol health probes with configurable interval and timeout;
- automatic initial connection retry and post-fault recovery;
- immediate, 1-second, 2-second, 5-second, and bounded 10-second reconnect delays;
- compatibility validation and complete readable-property resynchronization after reconnect;
- cached-property preservation while disconnected;
- cancellation-aware supervision and clean disposal;
- Protocol Explorer C-021 physical USB-disconnection and reconnection validation;
- compact property-write request and response wire contracts;
- descriptor-selected Boolean property-value encoding;
- descriptor-aware writable-property validation;
- endpoint-confirmed runtime property writing and cache synchronization;
- coordinator-owned writes serialized against replacement and disposal;
- Arduino Uno `Led.State` read/write firmware support;
- Protocol Explorer C-022 explicit property-writing validation.

Physical C-020 validation confirmed an empty initial cache, successful `Off` synchronization, successful toggle execution, replacement with `On`, UTC timestamps, `Good` quality, one runtime endpoint, and process exit code zero.

Physical C-021 validation confirmed `Disconnected -> Connecting -> Synchronizing -> Ready`, protocol-level USB-loss detection, `Ready -> Faulted`, bounded reconnect attempts while COM3 was absent, replacement connection establishment after the Arduino returned, complete property resynchronization, and restoration of `Ready` with a `Good` cached value. Ctrl+C produced `Ready -> Disconnected`, retained the final cached value, and exited with code zero.

Physical C-022 validation confirmed explicit `Off -> On -> Off` writes for `Led.State`. Every write and confirmation read returned `Success`; every confirmed runtime-cache value had a UTC timestamp and `Good` quality. Coordinator tests also confirmed that disposal waits for an active write and rejects writes queued behind disposal.

## 6.12 USB Serial Endpoint Discovery and Verification

**Status:** [Completed] Implemented and physically verified on Windows

Architecture: ADR-0021 - USB Serial Endpoint Discovery and Authoritative Compact Verification.

Implemented:

- platform-neutral USB serial candidate, filter, verifier, result, options, and orchestration contracts;
- Windows candidate enumeration through `System.Management` and `Win32_PnPEntity`;
- optional VID, PID, port, product, manufacturer, and USB serial-number filtering;
- read-only candidate enumeration with missing-metadata tolerance and malformed-record isolation;
- Windows port normalization and connection-target deduplication;
- sequential candidate verification using candidate-specific serial settings and explicit per-candidate timeouts;
- temporary `System.IO.Ports` connection ownership;
- existing Compact Serial Protocol V1 bootstrap without wire-contract changes;
- authoritative identity from `CompactBootstrapResponse.EndpointId`;
- exact versioned descriptor-reference resolution through the host repository;
- isolated busy, unavailable, access-denied, timeout, non-HASE, invalid-response, unsupported-version, invalid-identity, unknown-descriptor, and connection-failure outcomes;
- caller-cancellation propagation distinct from candidate failure;
- complete candidate-outcome retention;
- unique verified inventory deduplicated by authoritative `EndpointId`;
- production Windows discovery composition;
- Protocol Explorer `c023` physical validation;
- no automatic runtime attachment, replacement, publication, or mutation.

Physical validation confirmed:

```text
Candidate port         : COM10
VID                    : 0x2341
PID                    : 0x0043
Product                : Arduino Uno
USB serial number      : 75836333537351D06110
Result                 : Verified
Authoritative endpoint : arduino-uno-01
Descriptor reference   : arduino-uno-validation v1
Unique inventory       : 1
Runtime attachment     : None
Runtime mutation       : None
Verification streams   : Disposed
Process exit code       : 0
```

The COM port, VID, PID, product name, manufacturer, and USB serial number remain descriptive connection metadata. They are never substituted for HASE identity. The compact bootstrap response supplies the authoritative endpoint identity.

Manual COM-port configuration remains supported through the established compact connection path.

Linux USB serial discovery remains explicit backlog. The reserved incompatible-descriptor result requires a formal compact-profile contract before it is actively produced.

## 6.13 Compact Serial Endpoint Attachment

**Status:** [Completed] Implemented and physically verified on Windows

Capability C-024 extends the runtime-host-owned attachment lifecycle and
authoritative inventory to resource-constrained compact serial endpoints.

Implemented:

- configured and explicitly selected discovery-derived serial definitions
  converging on one compact attachment service;
- host-repository compact endpoint definitions combining one exact versioned
  descriptor reference, the complete descriptor content, and validated compact
  property mappings;
- temporary authoritative compact bootstrap before runtime construction;
- independent operational compact connection establishment;
- authoritative endpoint identity, descriptor, and operational-definition
  revalidation;
- initial readable-property synchronization before `Ready`;
- readiness-gated runtime publication and authoritative inventory insertion;
- shared native and compact successful-attachment lifecycle ownership;
- failed-attachment cleanup and cancellation propagation;
- recurring compact supervision, recovery, and resynchronization through the
  attached operational connection;
- duplicate authoritative-identity rejection without automatic replacement;
- explicit inventory detachment and orderly shutdown;
- production compact runtime-host composition;
- Protocol Explorer `c024` physical validation.

Physical validation confirmed:

```text
Candidate port         : COM3
VID                    : 0x2341
PID                    : 0x0043
Product                : Arduino Uno
Authoritative endpoint : arduino-uno-01
Descriptor reference   : arduino-uno-validation v1
Connection origin      : Discovered
Connection state       : Ready
Inventory entries      : 1
Published endpoints    : 1
Led.State              : False, UTC timestamp, Good quality
Detached               : True
Final state            : Disconnected
Final inventory        : 0
Final publication      : 0
Process exit code      : 0
```

Discovery verification, attachment bootstrap, and operational attachment use
three distinct connection ownership scopes. Discovery never attaches
automatically. USB metadata remains candidate information only, while compact
bootstrap supplies authoritative HASE identity. Configured and discovered
definitions use the same attachment path. Compact Serial Protocol Version 1 and
Protocol Version 1 remain unchanged.

## 6.14 Remaining Phase 6 Work

- validate Wi-Fi interruption and re-advertisement;
- keep verification sequential unless future physical evidence justifies bounded parallelism;
- implement and physically validate Linux USB serial discovery only through a separately approved increment;
- define a formal compact-profile compatibility contract before activating incompatible-descriptor classification;
- decide IPv6 scope;
- consider BLE and additional compact serial capabilities.

---

# Phase 7 - Application and Tooling Expansion

**Status:** [Planned] Planned

Possible scope:

- endpoint browser UI and explicit selection;
- runtime attachment workflow;
- topology and descriptor views;
- live properties and property editing;
- command execution and event monitoring;
- connection and transport diagnostics;
- configuration persistence.

Architecture constraint: discovery must never automatically replace an existing runtime endpoint.

---

# Future Transport Work

Possible transports and extensions include IPv6 mDNS/DNS-SD, Linux USB serial discovery, additional USB serial devices and metadata filters, BLE, MQTT, remote access, Tailscale-assisted discovery, gateway transports, additional compact value encodings, compact events, and formal compact profiles.

Transport implementations remain below Protocol Version 1.

---

# Future Protocol Work

Protocol Version 1 is frozen for the current endpoint contract. Authentication, authorization, encryption, capability negotiation, bulk operations, streaming, descriptor negotiation, compact profiles, and gateway routing require explicit future architecture decisions.

---

# Future Runtime and Simulation Work

Possible runtime work includes multiple attached endpoints, replacement policy, persistence, offline inventory, calibration, settings, EEPROM workflows, scheduling, and event history.

Possible simulation work includes noise, drift, calibration offsets, device and network failures, playback, scripted scenarios, and multi-endpoint simulation.

---

# Documentation Roadmap

Current documentation includes `Architecture.md`, `RuntimeArchitecture.md`, `RuntimeComponentModel.md`, `SerializationModel.md`, `ProjectStatus.md`, `Roadmap.md`, `C-023-USB-Serial-Endpoint-Discovery.md`, `C-024-Compact-Serial-Endpoint-Attachment.md`, and ADR-0001 through ADR-0021.

Next:

1. Keep physical capabilities C-015 through C-024 and their validation baselines current.
2. Keep IPv4 scope, IPv6 backlog, and Linux USB serial backlog explicit.
3. Keep candidate metadata separate from authoritative endpoint identity.
4. Keep the authoritative inventory identity and no-automatic-replacement rule explicit.
5. Record any future discovery concurrency or compact-profile decision in an ADR if it changes architecture.

---

# Current Priorities

1. Select the next Phase 6 capability explicitly.
2. Keep Linux USB serial discovery as a separately approved, platform-specific backlog item.
3. Define a formal compact-profile contract before activating incompatible-descriptor classification.
4. Keep IPv6, additional compact serial capabilities, BLE, remote APIs, and Tailscale host detection as separately approved future capabilities.

---

# Phase 6 Completion Criteria

Already achieved:

- transport abstraction and framed TCP;
- duplex protocol sessions;
- automatic recovery and health probing;
- physical endpoint integration;
- physical properties, commands, and events;
- event recovery;
- IPv4 network discovery;
- authoritative endpoint verification;
- C-016 explicit endpoint attachment and lifecycle ownership;
- automated and physical attachment lifecycle validation;
- C-017 authoritative runtime-host attachment inventory;
- deterministic duplicate, detachment, and disposal coordination;
- automated and physical host-inventory validation;
- USB serial transport and Compact Serial Protocol Version 1;
- C-018 compact bootstrap and descriptor resolution;
- C-019 compact command execution;
- C-020 compact property reading and runtime-cache synchronization;
- C-021 compact serial connection supervision, health probing, bounded recovery, resynchronization, cache preservation, and clean shutdown;
- C-022 compact property writing, endpoint confirmation, runtime-cache synchronization, coordinator lifecycle ownership, and physical Arduino Uno validation;
- C-023 Windows USB serial candidate enumeration, metadata filtering, authoritative compact bootstrap verification, exact descriptor resolution, isolated outcomes, unique endpoint inventory, production composition, and physical Arduino Uno validation.
- C-024 explicitly selected compact serial endpoint attachment through the runtime-host inventory, independent bootstrap and operational connections, authoritative revalidation, initial property synchronization, readiness-gated publication, supervision, and orderly physical Arduino Uno detachment.

Still requiring implementation or explicit scope decisions:

- whether IPv6 belongs in Phase 6;
- whether BLE belongs in Phase 6;
- which additional compact serial operations belong in Phase 6;
- Linux USB serial discovery and physical validation;
- a formal compact-profile compatibility contract;
- whether the northbound runtime-host API belongs in Phase 6 or Phase 7.





