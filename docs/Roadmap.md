# HASE Roadmap

## Vision

HASE is built in layers. Each completed phase becomes a stable foundation for the following phases, and architecture changes should become increasingly rare as the framework matures.

HASE provides transport-independent access to physical and simulated hardware instruments through a common descriptor, runtime, protocol, and tooling model.

---

# Phase 1 – Foundation

**Status:** ✅ Completed

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

# Phase 2 – Simulation

**Status:** ✅ Completed

Implemented:

- `Hase.Simulation`;
- simulation host and steps;
- environment state and simulation;
- value generators and periodic waveforms;
- simulated environment sensor;
- runtime integration and tests.

Future extensions include noise, calibration, playback, JSON scenarios, and fault injection.

---

# Phase 3 – Protocol Foundation

**Status:** ✅ Completed

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

# Phase 4 – Protocol Version 1

**Status:** ✅ Completed

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

# Phase 5 – Runtime Integration

**Status:** ✅ Completed

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

# Phase 6 – Transport Infrastructure and Physical Endpoint Integration

**Status:** 🚧 Active – major capabilities completed

Current baseline:

```text
861 automated tests
.NET solution builds
ESP32 firmware builds
Physical ESP32 endpoint verified
IPv4 network discovery verified
```

## 6.1 Transport Abstraction

**Status:** ✅ Completed

Implemented `Hase.Transport`, transport connection and factory contracts, duplex connections, lifecycle states, loopback migration, and contract tests.

## 6.2 Framed TCP Transport

**Status:** ✅ Completed

Implemented TCP options, connection factory, four-byte big-endian framing, payload validation, connection timeouts, duplex send/receive, invalidation, tracing, concurrency tests, and failure tests.

## 6.3 Runtime Transport Integration

**Status:** ✅ Completed

Implemented connection management, legacy and duplex protocol connections, protocol sessions and bindings, endpoint synchronization, and connection coordination.

## 6.4 Automatic Reconnection

**Status:** ✅ Completed

Implemented initial retry, transport replacement, bounded backoff, complete resynchronization, cached-value preservation, cancellation-aware supervision, and diagnostics.

```text
immediate
1 second
2 seconds
5 seconds
10 seconds maximum
```

## 6.5 Duplex Protocol Health Probing

**Status:** ✅ Completed

Implemented coordinator-owned probing, explicit timeouts, silent-loss detection, transport invalidation, recovery through the existing supervisor, one receive path, and physical ESP32 reset validation.

Architecture: ADR-0017.

## 6.6 Runtime Event Routing and Recovery

**Status:** ✅ Completed

Implemented unsolicited notification routing, observer subscriptions, runtime event routing, router migration, physical GPIO17 notification, post-recovery validation, and logical diagnostics across sessions.

## 6.7 Physical ESP32 Endpoint

**Status:** ✅ Completed for the current endpoint contract

Hardware includes the DOIT ESP32 DEVKITC V4 / ESP32-WROOM, BME280, GPIO controller, Wi-Fi, and framed TCP port 5000.

Physical discovery, descriptor access, property reads and writes, commands, events, supervision, reconnect, probing, resynchronization, and notification recovery are verified. Capabilities C-003 through C-014 are complete.

## 6.8 Network Endpoint Discovery

**Status:** ✅ Implemented and physically verified for IPv4

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
- authentication and authorization remain out of scope.

Physical capability C-015 covers IPv4 mDNS/DNS-SD discovery and Protocol Version 1 verification.

Next documentation step: ADR-0018.

## 6.9 Remaining Phase 6 Work

- validate reset behavior during active discovery;
- validate Wi-Fi interruption and re-advertisement;
- decide sequential versus bounded-parallel verification;
- define discovery-result lifetime and diagnostics;
- design explicit endpoint selection and runtime attachment;
- validate Linux discovery;
- decide IPv6 scope;
- consider BLE and USB serial transports.

---

# Phase 7 – Application and Tooling Expansion

**Status:** 📋 Planned

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

Possible runtime work includes explicit attachment, multiple endpoints, replacement policy, persistence, offline inventory, calibration, settings, EEPROM workflows, scheduling, and event history.

Possible simulation work includes noise, drift, calibration offsets, device and network failures, playback, scripted scenarios, and multi-endpoint simulation.

---

# Documentation Roadmap

Current documentation includes `Architecture.md`, `RuntimeArchitecture.md`, `RuntimeComponentModel.md`, `SerializationModel.md`, `ProjectStatus.md`, `Roadmap.md`, and ADR-0001 through ADR-0017.

Next:

1. Add ADR-0018 for mDNS/DNS-SD discovery.
2. Record physical capability C-015.
3. Record IPv4 scope and IPv6 backlog.
4. Document explicit runtime attachment when designed.

---

# Current Priorities

1. Restore and commit complete Project Status and Roadmap files.
2. Add ADR-0018.
3. Validate discovery recovery after ESP32 reset.
4. Validate advertisement recovery after Wi-Fi interruption.
5. Decide verification concurrency.
6. Validate Linux discovery.
7. Select the next approved Phase 6 capability.

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
- authoritative endpoint verification.

Still requiring explicit scope decisions:

- whether IPv6 belongs in Phase 6;
- whether BLE belongs in Phase 6;
- whether USB serial belongs in Phase 6;
- whether runtime attachment belongs in Phase 6 or Phase 7;
- whether Linux validation is required before closing Phase 6.
