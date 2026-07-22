# Project Status

## Project

**HASE - Hardware Access System Environment**

HASE is an open, modular framework for describing, discovering, communicating with, and controlling hardware instruments independently of transport technology.

---

# Overall Status

**Current Phase:** Phase 6 - Transport Infrastructure and Physical Endpoint Integration

The core architecture, runtime model, simulation framework, Protocol Version 1, Compact Serial Protocol Version 1, runtime integration, Protocol Explorer, production TCP and USB serial transports, duplex protocol infrastructure, endpoint synchronization, automatic connection recovery, active protocol health probing, runtime event routing, transport diagnostics, physical property access, physical command execution, physical event notification, IPv4 network endpoint discovery, explicit runtime-host-owned endpoint attachment, the runtime-host attachment inventory, compact runtime property synchronization, and compact serial connection supervision are implemented. C-016 and C-017 are validated through the physical ESP32/BME280 endpoint; C-018 through C-021 are validated through the physical Arduino Uno endpoint.

The current verified baseline is:

```text
1,290 automated tests passing
.NET solution builds
ESP32 firmware builds
Arduino Uno firmware builds
Physical ESP32 endpoint verified
Physical Arduino Uno endpoint verified
IPv4 mDNS/DNS-SD discovery verified
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

Protocol Version 1 supports Properties, Commands, and Events. It supports full embedded descriptors and compact descriptor references. Network-discovery metadata is not part of the Protocol Version 1 wire contract.

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
- physical C-017 inventory attachment and detachment validation.
- production USB serial byte transport for Arduino Uno-class endpoints;
- Compact Serial Protocol Version 1 framing, correlation, and CRC validation;
- versioned host-side compact endpoint descriptor resolution;
- physical C-018 compact bootstrap and descriptor-resolution validation;
- compact command execution and physical C-019 LED-toggle validation;
- descriptor-side compact property mappings and Boolean value decoding;
- compact property reads and physical C-020 LED-state validation;
- compact runtime property synchronization with cache-preservation semantics;
- compact serial connection ownership and coordinated connection replacement;
- recurring compact endpoint health probing with explicit interval and timeout controls;
- automatic compact serial recovery using immediate, 1-second, 2-second, 5-second, and bounded 10-second retry delays;
- cache preservation during compact serial faults and property refresh after recovery;
- clean cancellation-aware compact supervision shutdown;
- physical C-021 USB-disconnection detection, retry, reconnection, resynchronization, and shutdown validation.

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

It also contains the production serial byte-stream abstraction and System.IO.Ports implementation used by Arduino Uno-class compact endpoints.

## Runtime Transport Integration

`Hase.Runtime.Transport` contains connection management, runtime protocol connections, duplex sessions, protocol bindings, synchronization, recovery supervision, health probing, notification migration, candidate verification, discovery orchestration, endpoint attachment services, the authoritative attachment inventory, and runtime attachment-host composition.

It additionally contains compact runtime property synchronization and compact endpoint connection supervision. Successful compact reads update the existing `RuntimeProperty` cache; unsuccessful reads preserve the previous cached value. Compact connection ownership, coordination, health probing, recurring supervision, retry, replacement, resynchronization, and cancellation-aware disposal reuse the runtime endpoint connection-state model while remaining independent of the native Protocol Version 1 transport path.

## Compact Protocol

`Hase.CompactProtocol` contains the resource-constrained Compact Serial Protocol Version 1 defined by ADR-0020. Compact endpoints expose authoritative identity and a versioned descriptor reference while the complete descriptor remains in the runtime-host repository.

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

## Arduino Uno Compact Endpoint

```text
Board              : Arduino Uno class
Endpoint ID        : arduino-uno-01
Transport          : USB serial at 115200 baud
Protocol           : Compact Serial Protocol V1
Descriptor         : arduino-uno-validation v1
Property           : Led.State (compact id 0x01)
Command            : Led.Toggle (compact id 0x01)
```

The serial connection carries binary HASE frames exclusively. Compact bootstrap returns the authoritative endpoint identity and versioned descriptor reference. The runtime host resolves the complete descriptor from its repository.

Physical validation confirmed bootstrap and descriptor resolution (C-018), built-in LED command execution (C-019), Boolean LED-state synchronization into the existing runtime property cache before and after the toggle command (C-020), and automatic compact serial connection recovery after USB disconnection (C-021). The C-020 observed transition was `Off -> On`; both cached values had UTC timestamps and `Good` quality.

C-021 physical validation started from `Disconnected`, progressed through `Connecting` and `Synchronizing` to `Ready`, detected USB loss as `Faulted`, and retried using the configured bounded schedule. After the Arduino returned on the same COM port, supervision re-established the connection, resynchronized `Led.State`, restored `Ready`, and retained a `Good` cached value. Ctrl+C stopped supervision cleanly, transitioned the endpoint from `Ready` to `Disconnected`, preserved the final cached value, and exited with code 0.

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
    ->
DiscoverResponse
```

The `EndpointId` returned by `DiscoverResponse` is authoritative. Protocol Version 1 remains unchanged.

Candidate processing uses two deduplication stages:

1. mDNS candidates are deduplicated by address and port.
2. Verified endpoints are deduplicated by authoritative `EndpointId`.

Unreachable, timed-out, non-HASE, and invalid-response candidates are isolated. Caller cancellation remains distinct and stops browsing and active verification.

Network discovery does not create, attach, replace, or mutate runtime endpoints. Application or user selection is required before future attachment.

One discovery session produces a unique endpoint inventory. An endpoint that disappears and returns with the same address, port, and authoritative endpoint ID is not emitted again during that session.

Live presence tracking with Added, Updated, and Removed endpoint events is a separate future capability.

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
    ->
.NET mDNS browser
    ->
NetworkEndpointCandidate
    ->
Framed TCP connection
    ->
DiscoverRequest
    ->
DiscoverResponse
    ->
VerifiedNetworkEndpoint
```

A physical ESP32 reset test confirmed the unique-inventory behavior: after the endpoint returned with the same candidate and authoritative identity, Protocol Explorer did not display a duplicate result. Ctrl+C still stopped discovery cleanly in PowerShell.

---

# Local Endpoint Communication Lifecycle

ADR-0019 defines the HASE runtime host as the owner of the complete local communication lifecycle for each attached endpoint.

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

Discovery and manual configuration are equal sources of connection definitions. Detection never attaches, replaces, operates, or detaches a runtime endpoint automatically.

An attachment session exposes the attached runtime endpoint while owning its transport, protocol session, synchronization, coordination, recovery, notification routing, diagnostics, runtime publication, and orderly shutdown.

Native HASE endpoints are verified again through Protocol Version 1 on the connection that enters the operational lifecycle. Network addresses remain connection information rather than endpoint identity.

Descriptor resolution is independent of connection resolution. Complete endpoint descriptors, compact versioned repository references, and adapter-configured descriptors are supported architecture paths.

The local runtime host owns the physical TCP or future serial connection. Multiple instruments may share one endpoint connection. Future local or remote applications access those instruments through the runtime host rather than sharing the physical connection directly.

Tailscale-based runtime-host detection and a future northbound runtime API remain above and independent of the local endpoint lifecycle.

C-016 is complete for native framed-TCP endpoints. The implementation performs temporary bootstrap, authoritative identity and descriptor acquisition, staged runtime construction, independent operational connection establishment, operational identity revalidation, descriptor and readable-property synchronization, readiness-gated publication, recovery supervision, and orderly session shutdown.

Physical validation confirmed the bootstrap-to-operational handoff on the ESP32. `HaseTcpTransport` now accepts a newly pending client and replaces the previous stale client when the ESP32 network stack has not yet observed the bootstrap peer closure.

C-017 adds the runtime-host attachment inventory above the C-016 lifecycle. Inventory entries are keyed only by the authoritative `EndpointId` exposed by the attached `RuntimeEndpoint`. Attach, find, snapshot list, detach, and asynchronous disposal are coordinated through one host-owned inventory. Duplicate identity never replaces an existing attachment; the rejected session is cleaned up.

Automated framed-TCP validation covers bootstrap, operational attachment, readiness-gated publication, authoritative lookup, detachment, connection closure, and removal from both the attachment inventory and `RuntimeContext`. Physical validation confirmed one inventory entry and one published endpoint while Ready, followed by explicit detachment to `Disconnected` with both inventories empty.

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

Compact serial endpoints use `CompactRuntimeEndpointConnectionCoordinator` and `CompactRuntimeEndpointConnectionSupervisor` for the equivalent compact lifecycle. `CompactEndpointHealthProbe` performs recurring protocol-level reads through the owned compact connection, with a one-second probe interval and three-second timeout in the C-021 physical scenario. Probe failures or timeouts invalidate and detach the unusable connection before the established reconnect policy creates a replacement. Successful reconnection validates endpoint compatibility, refreshes readable compact properties, and returns the endpoint to `Ready` without discarding cached values during the fault.

---

# Protocol Notifications and Diagnostics

Unsolicited notifications share the duplex receive path with correlated responses. Runtime observers remain registered across transport and session replacement.

Diagnostics include transport state, health snapshots, connection and recovery statistics, logical exchange counts, byte counts, durations, cancellation and failure outcomes, replacement counts, recovery timing, and Protocol Explorer tracing.

---

# Capabilities

- C-001 - Runtime property access through Protocol Version 1.
- C-002 - Runtime event subscription and notification routing.
- C-003 through C-014 - Physical framed TCP, Protocol Version 1 operations, synchronization, recovery, probing, physical properties, commands, duplex notifications, router migration, and event recovery.
- C-015 - IPv4 mDNS/DNS-SD discovery with authoritative Protocol Version 1 endpoint verification.
- C-016 - Explicit native network endpoint attachment through the runtime-host lifecycle.
- C-017 - Runtime-host attachment inventory with authoritative identity, duplicate rejection, coordinated lifecycle ownership, and explicit detachment.
- C-018 - Physical compact serial bootstrap and host-side descriptor resolution for Arduino Uno-class endpoints.
- C-019 - Physical compact command execution through the Arduino Uno built-in LED.
- C-020 - Physical compact property reading and runtime-cache synchronization.
- C-021 - Compact serial connection supervision with health probing, bounded retry, connection replacement, resynchronization, cache preservation, and clean shutdown.

---

# Verification Status

```text
.NET solution builds
1,290 automated tests pass
ESP32 firmware builds
Arduino Uno firmware builds
BME280 initializes
Wi-Fi connects
UTC synchronizes
TCP server listens on port 5000
mDNS advertises _hase._tcp.local
Protocol Explorer discovers the candidate
DiscoverRequest/DiscoverResponse succeeds
Authoritative EndpointId is verified
Ctrl+C stops discovery cleanly
C-016 bootstrap and operational TCP sessions succeed
C-016 publishes only after Ready
C-016 synchronizes four physical readable properties
C-016 shutdown ends Disconnected with zero published endpoints
C-017 inventory lookup returns the same authoritative entry
C-017 inventory contains one entry while Ready
C-017 explicit detach returns True
C-017 detach ends Disconnected with zero inventory entries and zero published endpoints
C-018 compact bootstrap resolves arduino-uno-validation v1
C-019 compact LED-toggle command returns Success
C-020 synchronizes Led.State from Off to On in the runtime cache
C-021 detects physical Arduino USB disconnection and enters Faulted
C-021 retries compact serial connection establishment with bounded backoff
C-021 reconnects on the configured COM port and returns through Synchronizing to Ready
C-021 preserves the cached Led.State value while faulted and refreshes it after recovery
C-021 Ctrl+C shutdown ends Disconnected with the final cached value retained
C-021 Protocol Explorer exits with code 0
```

---

# Architecture Decision Records

ADR-0001 through ADR-0020 are accepted. ADR-0017 defines duplex protocol health probing. ADR-0018 defines mDNS/DNS-SD network endpoint discovery and authoritative Protocol Version 1 candidate verification. ADR-0019 defines local endpoint communication lifecycle ownership. ADR-0020 defines the resource-constrained serial endpoint protocol.

---

# Current Limitations

The current implementation intentionally excludes IPv6 discovery, live Added/Updated/Removed presence tracking, authentication, authorization, encryption, automatic attachment without an explicit request, automatic endpoint replacement, cross-subnet mDNS relaying, parallel candidate verification, persistent discovery results, Linux physical validation, BLE, automatic serial-device identification, compact property writes, compact event notifications, and additional compact scalar encodings.

---

# Immediate Next Steps

1. Keep C-016 through C-021 physical validation baselines current.
2. Select the next Phase 6 capability explicitly.
3. Decide whether Linux validation is required before closing Phase 6.
4. Keep live presence tracking, additional compact operations, BLE, remote APIs, and Tailscale host detection in backlog until their capabilities are explicitly approved.

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



