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
998 automated tests
.NET solution builds
ESP32 firmware builds
Physical ESP32 endpoint verified
IPv4 network discovery verified
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

## 6.11 Remaining Phase 6 Work

- validate Wi-Fi interruption and re-advertisement;
- decide sequential versus bounded-parallel verification;
- validate Linux discovery;
- decide IPv6 scope;
- consider serial, BLE, and USB transports.

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

Possible transports and extensions include IPv6 mDNS/DNS-SD, USB serial and CH340 identification, BLE, MQTT, remote access, Tailscale-assisted discovery, gateway transports, and resource-constrained microcontroller transports.

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

Current documentation includes `Architecture.md`, `RuntimeArchitecture.md`, `RuntimeComponentModel.md`, `SerializationModel.md`, `ProjectStatus.md`, `Roadmap.md`, and ADR-0001 through ADR-0019.

Next:

1. Keep physical capabilities C-015 through C-017 and their validation baselines current.
2. Keep IPv4 scope and IPv6 backlog explicit.
3. Keep the authoritative inventory identity and no-automatic-replacement rule explicit.
4. Record any future discovery concurrency decision in an ADR if it changes architecture.

---

# Current Priorities

1. Select the next Phase 6 capability explicitly.
2. Decide whether Linux validation is required before Phase 6 completion.
3. Keep IPv6, serial transport, BLE, remote APIs, and Tailscale host detection as separately approved future capabilities.

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
- automated and physical host-inventory validation.

Still requiring implementation or explicit scope decisions:

- whether IPv6 belongs in Phase 6;
- whether BLE belongs in Phase 6;
- whether USB serial belongs in Phase 6;
- whether Linux validation is required before closing Phase 6;
- whether the northbound runtime-host API belongs in Phase 6 or Phase 7.




