# Project Status

## Project

**HASE – Hardware Access System Environment**

HASE is an open, modular framework for describing, discovering, communicating
with, and controlling hardware instruments independently of transport
technology.

---

# Overall Status

**Current Phase:** Phase 6 – Transport Infrastructure and Physical Endpoint Integration

The core architecture, runtime model, simulation framework, Protocol Version 1,
runtime integration, Protocol Explorer, production transport lifecycle, runtime
endpoint synchronization, and automatic connection recovery are implemented.

Phase 6 has established:

- the first real TCP transport;
- the first complete physical HASE endpoint based on an ESP32 and BME280;
- explicit transport connection lifecycle and health tracking;
- runtime endpoint connection coordination;
- strict endpoint-descriptor compatibility validation;
- readable-property synchronization;
- automatic initial connection retry;
- automatic recovery after transport faults;
- complete descriptor and property resynchronization after reconnect;
- preservation of cached property values while disconnected;
- successful and failed end-to-end runtime, protocol, transport, and reconnect
  integration tests.

The project can discover a physical endpoint, retrieve its descriptor, read live
engineering properties, validate complete request/response transactions through
Protocol Version 1 over framed TCP, establish and supervise a managed runtime
connection, validate the physical descriptor, populate readable runtime property
caches, and publish `Ready` only after synchronization completes successfully.

If the initial connection fails, supervision continues according to the
configured retry policy. If an established transport connection faults, HASE
replaces the failed transport, retrieves and validates the descriptor again,
resynchronizes all readable properties, and returns the endpoint to `Ready`.

---

# Completed Phases

## Phase 1 – Foundation

**Status:** Completed

Highlights:

- Core domain model
- Runtime model
- Descriptor model
- Identity model
- Architecture documentation
- Initial architecture decision records
- Unit-test infrastructure

---

## Phase 2 – Simulation

**Status:** Completed

Highlights:

- Simulation framework
- Simulation host
- Environment simulation
- Value-generator hierarchy
- Simulated instruments
- Runtime integration
- Simulation test suite

---

## Phase 3 – Protocol Foundation

**Status:** Completed

Highlights:

- Protocol architecture
- Protocol message model
- Binary serialization foundation
- Session model
- Runtime component model
- Serialization architecture
- Protocol design decision records

---

## Phase 4 – Protocol Version 1

**Status:** Completed

Highlights:

### Binary Protocol

- BinaryProtocolReader
- BinaryProtocolWriter
- BinaryProtocolPayloadCodec
- ProtocolEnvelopeByteCodec

### Serialization

- Descriptor serializers
- VariantSerializer
- PropertyValueSerializer
- Protocol serialization helpers

### Protocol Messages

Implemented support for:

- Discover
- ReadEndpointDescriptor
- ReadProperty
- WriteProperty
- ExecuteCommand
- EventNotification

Protocol Version 1 is feature complete for the currently defined message set.

---

## Phase 5 – Runtime Integration and Protocol Explorer

**Status:** Completed

Highlights:

- Runtime protocol dispatcher
- Runtime request routing
- Property read and write routing
- Command execution routing
- Runtime service integration
- Runtime-backed capability demonstrations
- Protocol Explorer
- Annotated payload visualization
- Byte-oriented loopback transport
- Shared capability-scenario framework
- End-to-end runtime protocol execution

Phase 5 completion baseline:

- **428 automated tests passing**

---

# Current Focus

## Phase 6 – Transport Infrastructure and Physical Endpoint Integration

**Status:** In Progress

### Completed Transport Foundations

- Protocol-independent `ITransportConnection`
- Byte-oriented request/response exchange
- Loopback transport
- TCP transport
- TCP transport options and factory
- Framed TCP communication
- Maximum-payload validation
- Cancellation and timeout propagation
- Transport exception propagation
- Physical Protocol Explorer execution
- Reusable `ProtocolEnvelopeByteCodec` in `Hase.Protocol`
- Removal of the Protocol Explorer framing-code duplicate

### Completed Transport Lifecycle Foundations

- Explicit transport connection states
- Transport health snapshots
- Transport health-change notifications
- `TransportConnectionManager`
- Current-connection ownership
- Faulted-connection replacement
- Connection-state timestamps
- Connection disposal behavior
- Fault propagation
- Repeated-disposal safety
- Runtime endpoint connection coordination
- Explicit runtime endpoint lifecycle states:
  - `Disconnected`
  - `Connecting`
  - `Synchronizing`
  - `Ready`
  - `Reconnecting`
  - `Faulted`
- Separation of transport connection failure from synchronization failure
- Cancellation handling for connection and synchronization
- Runtime status updates after transport fault or closure

### Completed Runtime Endpoint Synchronization

- `IRuntimeEndpointSynchronizer`
- `ProtocolRuntimeEndpointSynchronizer`
- Physical endpoint-descriptor request during connection
- Protocol message-type validation
- Correlation-identifier validation
- Protocol-result validation
- Successful-response descriptor-presence validation
- Strict physical/runtime descriptor compatibility validation
- Canonical protocol-representation comparison
- Readable-property enumeration
- `Read` property synchronization
- `ReadWrite` property synchronization
- Write-only property exclusion
- Runtime property-cache population
- Runtime property observer notification
- Property quality preservation
- Protocol timestamp precision handling
- Sequential property synchronization
- Partial synchronization semantics
- Cancellation between property reads
- Preservation of values received before a later failure
- `Ready` publication only after descriptor and property synchronization complete

### Completed Automatic Connection Recovery

- `IRuntimeEndpointReconnectPolicy`
- `DefaultRuntimeEndpointReconnectPolicy`
- `RuntimeEndpointConnectionSupervisor`
- Immediate first retry
- One-second second retry
- Two-second third retry
- Five-second fourth retry
- Ten-second maximum retry delay
- Automatic retry after initial connection failure
- Automatic detection of established transport faults
- Automatic replacement of faulted transport connections
- Reuse of a connected transport after synchronization-only failure
- Complete descriptor retrieval after reconnect
- Complete descriptor compatibility validation after reconnect
- Complete readable-property synchronization after reconnect
- Retry after transport replacement failure
- Retry after synchronization failure
- Retry-attempt reset after successful recovery
- Cached-property preservation while faulted
- Cached-property preservation while reconnecting
- Cached-property preservation after supervision cancellation
- One supervision task per supervisor instance
- Cancellation during initial connection
- Cancellation during transport replacement
- Cancellation during endpoint synchronization
- Cancellation during retry delay
- Cancellation ending in `Disconnected`

### Completed Runtime Transport Integration Tests

- Real coordinator with real protocol synchronizer
- Real binary payload codec
- Real protocol-envelope byte codec
- Real loopback byte transport
- Real runtime protocol dispatcher
- Successful descriptor synchronization
- Successful property synchronization
- Verification that the runtime cache is populated before `Ready`
- Property synchronization failure propagation
- Verification that failed synchronization prevents `Ready`
- Verification that failed synchronization ends in `Faulted`
- Verification that an established transport remains available after a
  synchronization failure
- Faulted transport replacement
- Reconnect cancellation behavior
- Reconnect replacement-failure behavior
- Reconnect synchronization-failure behavior
- Synchronization retry over an already-connected transport
- Supervisor single-task behavior
- Initial connection retry behavior
- Repeated reconnect retry behavior
- Retry-attempt reset behavior
- Retry-delay cancellation behavior
- Real-protocol automatic reconnect
- Descriptor reread after automatic reconnect
- Property reread after automatic reconnect
- Cache preservation during automatic reconnect
- Cache update after successful resynchronization

### Completed Physical Endpoint Foundations

- ESP32 endpoint application
- DOIT ESP32 DEVKIT V4 hardware target
- BME280 connected through I²C
- GPIO21 used for SDA
- GPIO22 used for SCL
- BME280 address `0x76`
- Wi-Fi connectivity
- TCP server on port `5000`
- HASE Protocol Version 1 envelope decoding
- Protocol request dispatch
- Physical endpoint descriptor
- Physical instrument descriptor
- Property request decoding
- Physical property-service abstraction
- Property-response serialization
- UTC synchronization through SNTP
- Unix timestamps in milliseconds
- Property quality reporting
- Failure-result serialization

### Completed Physical Capabilities

The physical endpoint supports complete request/response reads for:

- `physical.environment-sensor.temperature`
- `physical.environment-sensor.relative-humidity`
- `physical.environment-sensor.air-pressure`

Each successful property response contains:

- A `double` engineering value
- A truthful UTC timestamp
- `PropertyQuality.Good`
- The original request correlation identifier

### Protocol Explorer Physical Scenarios

Completed scenarios include:

- C-003 physical TCP connectivity
- C-004 physical discovery
- C-005 physical endpoint-descriptor exchange
- C-006 physical property reads

C-006 validates:

- Temperature
- Relative humidity
- Air pressure
- Protocol result
- Correlation identifiers
- Variant type
- UTC timestamps
- Property quality
- Plausible engineering ranges
- Round-trip communication

### Phase 6 Quality Baseline

- **616 automated tests passing**

---

# Current Architecture

Implemented components:

- Hase.Core
- Hase.Runtime
- Hase.Simulation
- Hase.Protocol
- Hase.Transport
- Hase.Runtime.Transport
- HASE.ProtocolExplorer
- ESP32 physical endpoint

The architecture currently provides:

- Transport-independent runtime abstractions
- Protocol-independent transport abstractions
- Binary Protocol Version 1
- Simulation and physical endpoint execution
- Descriptor-driven endpoint discovery
- Live physical property access
- Managed runtime transport connections
- Runtime endpoint lifecycle coordination
- Automatic connection supervision
- Configurable reconnect policy
- Strict physical/runtime descriptor validation
- Runtime property-cache synchronization
- Cache preservation across temporary connection failures
- Layered separation between protocol, transport, runtime coordination,
  physical services, and hardware

The current initial connection and synchronization flow is:

```text
RuntimeEndpointConnectionSupervisor
        ↓
RuntimeEndpointConnectionCoordinator.ConnectAsync
        ↓
TransportConnectionManager.ConnectAsync
        ↓
ProtocolRuntimeEndpointSynchronizer
        ↓
ReadEndpointDescriptorRequest
        ↓
EndpointDescriptorCompatibilityValidator
        ↓
ReadPropertyRequest for every readable property
        ↓
RuntimeProperty.CurrentValue update
        ↓
EndpointConnectionState.Ready