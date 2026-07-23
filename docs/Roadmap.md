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

**Status:** [Active] Active - major capabilities completed

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
Candidate port         : COM10
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
Connecting -> Faulted  (COM10 unavailable)
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

## 6.15 Remaining Phase 6 Work

- validate Wi-Fi interruption and re-advertisement if still required for Phase 6;
- keep USB serial candidate verification sequential unless physical evidence
  justifies bounded parallelism;
- implement and physically validate Linux USB serial discovery only through a
  separately approved increment;
- define a formal compact-profile compatibility contract before activating
  incompatible-descriptor classification;
- decide IPv6 scope;
- decide BLE scope;
- select any additional compact operations as separate capabilities;
- decide whether the northbound runtime-host API belongs in Phase 6 or Phase 7.

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
- configuration persistence;
- northbound runtime-host APIs if not completed earlier.

Architecture constraint: discovery must never automatically replace an existing
runtime endpoint.

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
- ADR-0001 through ADR-0022.

Next:

1. Keep physical capabilities C-015 through C-025 and their validation baselines
   current.
2. Keep IPv4 scope, IPv6 backlog, and Linux USB serial backlog explicit.
3. Keep candidate metadata separate from authoritative endpoint identity.
4. Keep authoritative inventory identity and no-automatic-replacement rules
   explicit.
5. Keep compact current-connection event authority, no-queue, and no-replay
   semantics explicit.
6. Record any future compact-profile, discovery-concurrency, or event-history
   architecture change in an ADR.

---

# Current Priorities

1. Select the next Phase 6 capability explicitly.
2. Keep Linux USB serial discovery as a separately approved platform-specific
   backlog item.
3. Define a formal compact-profile contract before activating
   incompatible-descriptor classification.
4. Keep IPv6, additional compact operations, BLE, remote APIs, and Tailscale
   host detection as separately approved future capabilities.

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

Still requiring implementation or explicit scope decisions:

- whether IPv6 belongs in Phase 6;
- whether BLE belongs in Phase 6;
- which additional compact operations belong in Phase 6;
- Linux USB serial discovery and physical validation;
- a formal compact-profile compatibility contract;
- whether the northbound runtime-host API belongs in Phase 6 or Phase 7.
