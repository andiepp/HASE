# Project Status

## Project

**HASE - Hardware Access System Environment**

HASE is an open, modular framework for describing, discovering, communicating
with, and controlling hardware instruments independently of transport
technology.

---

# Overall Status

**Current Phase:** Phase 6 - Transport Infrastructure and Physical Endpoint
Integration

The core architecture, runtime model, simulation framework, Protocol Version 1,
Compact Serial Protocol Version 1, runtime integration, Protocol Explorer,
production TCP and USB serial transports, duplex protocol infrastructure,
endpoint synchronization, automatic connection recovery, active protocol health
probing, runtime event routing, transport diagnostics, physical property access,
physical command execution, physical event notification, IPv4 network endpoint
discovery, explicit runtime-host-owned endpoint attachment, the authoritative
runtime-host attachment inventory, compact runtime property synchronization,
compact serial connection supervision, Windows USB serial discovery, compact
serial endpoint attachment, and compact serial unsolicited event notification
are implemented.

C-016 and C-017 are validated through the physical ESP32/BME280 endpoint.
C-018 through C-025 are validated through the physical Arduino Uno endpoint.

The current verified baseline is:

```text
1,745 automated tests passing
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

## Compact Protocol

`Hase.CompactProtocol` contains the resource-constrained Compact Serial Protocol
Version 1 defined by ADR-0020 and extended for unsolicited events by ADR-0022.

Compact endpoints expose authoritative identity and a versioned descriptor
reference while the complete descriptor and compact property/event mappings
remain in the runtime-host repository.

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

The verified IPv4 address during physical discovery was `192.168.0.223`. The
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
  reset recovery, and USB unplug/replug recovery.

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

Physical USB removal produced unavailable-port reconnect failures until COM10
returned:

```text
Ready -> Faulted
Faulted -> Connecting
Connecting -> Faulted  (COM10 unavailable)
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

---

# Verification Status

```text
.NET solution builds
1,745 automated tests pass
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
```

---

# Architecture Decision Records

ADR-0001 through ADR-0022 are accepted.

Relevant recent decisions:

- ADR-0017 - Duplex Protocol Health Probing.
- ADR-0018 - mDNS/DNS-SD Network Endpoint Discovery.
- ADR-0019 - Local Endpoint Communication Lifecycle Ownership.
- ADR-0020 - Resource-Constrained Serial Endpoint Protocol.
- ADR-0021 - USB Serial Endpoint Discovery and Authoritative Compact
  Verification.
- ADR-0022 - Compact Serial Event Notifications.

---

# Current Limitations

The current implementation intentionally excludes:

- IPv6 discovery;
- live Added/Updated/Removed presence tracking;
- authentication, authorization, and encryption;
- automatic attachment without an explicit request;
- automatic endpoint replacement;
- cross-subnet mDNS relaying;
- parallel candidate verification;
- persistent discovery results;
- Linux USB serial discovery and physical validation;
- BLE;
- formal compact-profile negotiation;
- persistent event history and replay;
- additional compact scalar/event-value encodings;
- northbound runtime-host APIs;
- Tailscale runtime-host discovery.

---

# Immediate Next Steps

1. Keep C-016 through C-025 physical validation baselines current.
2. Select the next Phase 6 capability explicitly.
3. Keep Linux USB serial discovery and physical validation explicit backlog.
4. Define a formal compact-profile contract before activating
   incompatible-descriptor classification.
5. Keep live presence tracking, additional compact operations, BLE, remote APIs,
   and Tailscale host detection in backlog until explicitly approved.

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
