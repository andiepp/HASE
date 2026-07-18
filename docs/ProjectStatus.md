# Project Status

## Project

**HASE â€“ Hardware Access System Environment**

HASE is an open, modular framework for describing, discovering, communicating with, and controlling hardware instruments independently of transport technology.

---

# Overall Status

**Current Phase:** Phase 6 â€“ Transport Infrastructure and Physical Endpoint Integration

The core architecture, runtime model, simulation framework, Protocol Version 1, runtime integration, Protocol Explorer, production TCP transport, duplex protocol infrastructure, endpoint synchronization, automatic connection recovery, active protocol health probing, runtime event routing, transport diagnostics, physical property access, physical command execution, physical event notification, and IPv4 network endpoint discovery are implemented.

The current verified baseline is:

```text
861 automated tests passing
.NET solution builds
ESP32 firmware builds
Physical ESP32 endpoint verified
IPv4 mDNS/DNS-SD discovery verified
```

Protocol Version 1 is feature complete for the current endpoint contract.

---

# Completed Phases

## Phase 1 â€“ Foundation

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

## Phase 2 â€“ Simulation

Completed:

- `Hase.Simulation`;
- simulation host and simulation steps;
- environment simulation and environment state;
- value-generator hierarchy;
- periodic waveform generators;
- simulated environment sensor;
- simulation/runtime integration;
- simulation tests.

## Phase 3 â€“ Protocol Foundation

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

## Phase 4 â€“ Protocol Version 1

Completed:

- `DiscoverRequest` and `DiscoverResponse`;
- `ReadEndpointDescriptorRequest` and `ReadEndpointDescriptorResponse`;
- `ReadPropertyRequest` and `ReadPropertyResponse`;
- `WritePropertyRequest` and `WritePropertyResponse`;
- `ExecuteCommandRequest` and `ExecuteCommandResponse`;
- `EventNotification`.

Protocol Version 1 supports Properties, Commands, and Events. It supports full embedded descriptors and compact descriptor references. Network-discovery metadata is not part of the Protocol Version 1 wire contract.

## Phase 5 â€“ Runtime Integration

Completed:

- runtime protocol dispatcher;
- property, command, and event routing;
- runtime protocol client;
- loopback protocol integration;
- Protocol Explorer;
- logical, message, and byte tracing;
- end-to-end runtime capability tests.

## Phase 6 â€“ Transport Infrastructure and Physical Endpoint Integration

Phase 6 is active and substantially implemented.

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
- Protocol Explorer network-discovery scenario.

---

# Current Architecture

## Core Model

`Hase.Core` contains transport-independent identities, descriptors, paths, quantities, units, and endpoint, instrument, property, command, and event definitions.

## Runtime Model

`Hase.Runtime` contains runtime contexts, endpoints, instruments, properties, property caches, connection status, command execution, protocol dispatch, and event routing.

The physical endpoint remains authoritative. The runtime maintains a synchronized local representation and preserves cached values during temporary disconnection.

## Protocol

`Hase.Protocol` contains Protocol Version 1 messages, envelopes, codecs, and serializers. It remains independent of TCP, mDNS, DNS-SD, ESP32, and runtime discovery policy.

## Transport

`Hase.Transport` contains the transport contracts, loopback transport, framed TCP transport, transport tracing, connection invalidation, network discovery contracts, and the IPv4 mDNS/DNS-SD browser.

## Runtime Transport Integration

`Hase.Runtime.Transport` contains connection management, runtime protocol connections, duplex sessions, protocol bindings, synchronization, recovery supervision, health probing, notification migration, candidate verification, and discovery orchestration.

---

# Physical Endpoint

```text
Board       : DOIT ESP32 DEVKITC V4 / ESP32-WROOM
Endpoint ID : doit-esp32-devkitc-v4-01
TCP port    : 5000
Protocol    : HASE Protocol Version 1
Transport   : Framed TCP
Discovery   : _hase._tcp.local
IP target   : IPv4
```

The verified IPv4 address during physical discovery was `192.168.0.223`. This address is discovered dynamically and is not authoritative identity.

## Environment Sensor Instrument

The BME280 instrument exposes Temperature, Relative Humidity, and Air Pressure. It uses GPIO21 for SDA, GPIO22 for SCL, and I2C address `0x76`.

## GPIO Controller Instrument

The GPIO controller exposes Boolean properties, commands, and events. Physical GPIO17 notification was validated through the complete duplex path and after connection recovery.

---

# Network Discovery

HASE uses mDNS with DNS-Based Service Discovery.

```text
Service type : _hase._tcp.local
Instance     : doit-esp32-devkitc-v4-01
TCP port     : 5000
```

mDNS/DNS-SD advertises reachability only. The service instance, host name, address, port, and TXT metadata are not authoritative HASE identity.

Every candidate is verified through the existing Protocol Version 1 exchange:

```text
DiscoverRequest
    â†’
DiscoverResponse
```

The `EndpointId` returned by `DiscoverResponse` is authoritative. Protocol Version 1 remains unchanged.

Candidate processing uses two deduplication stages:

1. mDNS candidates are deduplicated by address and port.
2. Verified endpoints are deduplicated by authoritative `EndpointId`.

Unreachable, timed-out, non-HASE, and invalid-response candidates are isolated. Caller cancellation remains distinct and stops browsing and active verification.

Network discovery does not create, attach, replace, or mutate runtime endpoints. Application or user selection is required before future attachment.

The first implementation accepts IPv4 candidates. IPv6 remains backlog.

Physical validation produced:

```text
Service  : doit-esp32-devkitc-v4-01
Candidate: 192.168.0.223:5000
Result   : Verified
Endpoint : doit-esp32-devkitc-v4-01
```

The verified path was:

```text
ESP32 mDNS advertisement
    â†’
.NET mDNS browser
    â†’
NetworkEndpointCandidate
    â†’
Framed TCP connection
    â†’
DiscoverRequest
    â†’
DiscoverResponse
    â†’
VerifiedNetworkEndpoint
```

---

# Connection and Recovery

`RuntimeEndpointConnectionCoordinator` owns the active runtime protocol binding and duplex-session lifecycle. It coordinates transport establishment, binding creation, synchronization, replacement, status transitions, notification routing, and logical diagnostics.

`RuntimeEndpointConnectionSupervisor` provides immediate connection attempts, retry, bounded backoff, replacement, full resynchronization, and cancellation-aware shutdown.

```text
immediate
1 second
2 seconds
5 seconds
10 seconds maximum
```

Active health probes use the existing duplex session, never introduce a competing reader, apply explicit timeouts, invalidate unusable transports, and trigger the established recovery path.

After reconnection, HASE performs descriptor synchronization, compatibility validation, readable-property refresh, binding replacement, and notification-router migration. Cached values remain available while disconnected.

---

# Protocol Notifications and Diagnostics

Unsolicited notifications share the duplex receive path with correlated responses. Runtime observers remain registered across transport and session replacement.

Diagnostics include transport state, health snapshots, connection and recovery statistics, logical exchange counts, byte counts, durations, cancellation and failure outcomes, replacement counts, recovery timing, and Protocol Explorer tracing.

---

# Capabilities

- C-001 â€“ Runtime property access through Protocol Version 1.
- C-002 â€“ Runtime event subscription and notification routing.
- C-003 through C-014 â€“ Physical framed TCP, Protocol Version 1 operations, synchronization, recovery, probing, physical properties, commands, duplex notifications, router migration, and event recovery.
- C-015 â€“ IPv4 mDNS/DNS-SD discovery with authoritative Protocol Version 1 endpoint verification.

---

# Verification Status

```text
.NET solution builds
861 automated tests pass
ESP32 firmware builds
BME280 initializes
Wi-Fi connects
UTC synchronizes
TCP server listens on port 5000
mDNS advertises _hase._tcp.local
Protocol Explorer discovers the candidate
DiscoverRequest/DiscoverResponse succeeds
Authoritative EndpointId is verified
Ctrl+C stops discovery cleanly
```

---

# Architecture Decision Records

ADR-0001 through ADR-0017 are accepted. ADR-0017 defines duplex protocol health probing. The implemented mDNS/DNS-SD discovery architecture will be recorded as ADR-0018.

---

# Current Limitations

The current implementation intentionally excludes IPv6 discovery, authentication, authorization, encryption, automatic runtime attachment, automatic endpoint replacement, cross-subnet mDNS relaying, parallel candidate verification, persistent discovery results, Linux physical validation, BLE, and USB serial transport.

---

# Immediate Next Steps

1. Add ADR-0018 for mDNS/DNS-SD network endpoint discovery.
2. Record capability C-015 in the architecture documentation.
3. Validate discovery behavior during ESP32 reset and Wi-Fi recovery.
4. Decide whether sequential verification is sufficient.
5. Validate discovery on Linux.
6. Continue Phase 6 only after explicit scope approval.

---

# Project Principles

- Architecture changes require explicit decisions.
- Protocol Version 1 remains transport-independent.
- The physical endpoint is authoritative.
- Discovery metadata is not identity.
- Runtime state is synchronized from the endpoint.
- Cached values remain available during disconnection.
- One duplex session owns the receive path.
- Increments remain small, buildable, and testable.
- Physical capabilities receive end-to-end validation.
- Discovered endpoints never replace active runtime endpoints automatically.