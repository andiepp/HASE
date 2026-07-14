# HASE Roadmap

This roadmap describes the planned evolution of HASE from the core runtime through protocol support, transport infrastructure, gateways, tooling, and the SDK.

---

# Phase 1 — Foundation

**Status:** Completed

## Objective

Establish the core domain model and runtime architecture.

### Completed

- Hase.Core
- Hase.Runtime
- Runtime graph
- Runtime notifications
- Discovery abstraction
- Runtime descriptor model
- Architecture documentation
- ADR-0001 through ADR-0007
- Comprehensive unit tests

---

# Phase 2 — Simulation

**Status:** Completed

## Objective

Provide a complete simulation environment for developing and testing HASE without physical hardware.

### Completed

- Hase.Simulation
- Simulation host
- Simulation time model
- Immutable environment state
- Constant value generator
- Periodic value generator
- Sine waveform
- Triangle waveform
- Sawtooth waveform
- Square waveform
- Environment simulation
- Simulated environment sensor
- Runtime integration
- Comprehensive simulation tests

### Future Extensions

- Interpolated waveforms
- Recorded time-series playback
- Noise and drift simulation
- Calibration models
- Quantization effects
- JSON simulation scenarios
- Shared simulation environments
- Simulation recording and replay

---

# Phase 3 — Protocol Foundation

**Status:** Completed

## Objective

Define the HASE protocol architecture and establish its binary serialization foundation.

### Completed

- Protocol message model
- Protocol request and response abstractions
- Protocol message types
- Binary protocol reader
- Binary protocol writer
- Binary payload encoding
- Protocol serialization helpers
- Descriptor-path serialization
- Protocol error handling
- Protocol architecture documentation

---

# Phase 4 — Protocol Implementation

**Status:** Completed

## Objective

Implement Protocol Version 1 for discovery, descriptors, properties, commands, and events.

### Protocol Infrastructure

- BinaryProtocolReader
- BinaryProtocolWriter
- BinaryProtocolPayloadCodec
- ProtocolSerializationHelper

### Descriptor Serialization

- EndpointDescriptorSerializer
- EndpointMetadataSerializer
- InstrumentDescriptorSerializer
- InstrumentMetadataSerializer
- InstrumentInterfaceSerializer
- PropertyDescriptorSerializer
- CommandDescriptorSerializer
- EventDescriptorSerializer
- DataDescriptorSerializer

### Runtime Serialization

- VariantSerializer
- PropertyValueSerializer

### Implemented Messages

#### Discovery

- DiscoverRequest
- DiscoverResponse

#### Descriptor Access

- ReadEndpointDescriptorRequest
- ReadEndpointDescriptorResponse

#### Property Access

- ReadPropertyRequest
- ReadPropertyResponse
- WritePropertyRequest
- WritePropertyResponse

#### Command Execution

- ExecuteCommandRequest
- ExecuteCommandResponse

#### Event Distribution

- EventNotification

### Testing

- Binary protocol verification
- Round-trip serialization tests
- Error handling tests
- Boundary-condition tests

Protocol Version 1 is feature complete.

---

# Phase 5 — Runtime Integration and Protocol Explorer

**Status:** Completed

## Objective

Connect Protocol Version 1 to the runtime, demonstrate runtime capabilities, and establish byte-oriented protocol exploration and tracing.

### Runtime Integration

- Protocol dispatcher
- Runtime request routing
- Property providers
- Command handlers
- Runtime service integration
- Runtime endpoint hosting foundations
- Runtime instrument integration
- End-to-end protocol dispatch tests

### Capability Demonstrations

- C-001 property capability demonstration
- C-002 command capability demonstration
- Shared capability scenario framework
- Scenario runner
- Protocol scenario base

### Protocol Explorer

- Protocol message visualization
- Request and response visualization
- Annotated payload visualization
- Protocol scenario execution
- Runtime capability demonstrations
- Byte-oriented loopback execution

### Transport Foundation

- Hase.Transport project
- ITransportConnection
- Protocol-independent byte exchange
- LoopbackTransportConnection
- Loopback transport tests
- Separation of protocol, runtime, and transport layers

### Testing

- Runtime protocol-dispatch tests
- Capability scenario tests
- Protocol Explorer integration tests
- Loopback transport contract tests

### Completion Baseline

- **428 automated tests passing**

---

# Phase 6 — Transport Infrastructure

**Status:** In Progress

## Objective

Provide production-ready, protocol-independent communication infrastructure between HASE runtimes and endpoints.

The transport layer exchanges byte sequences and remains independent of the HASE protocol and runtime models.

### Completed Foundations

- Hase.Transport
- ITransportConnection
- Byte-oriented request/response exchange
- LoopbackTransportConnection
- Request forwarding
- Cancellation-token propagation
- Null-request validation
- Null-response validation
- Transport exception propagation
- Loopback transport contract tests

### Planned Infrastructure

- Shared transport contract testing
- Transport lifecycle semantics
- Transport configuration models
- Transport creation and selection
- Connection management
- Automatic reconnect
- Cancellation and timeout handling
- Transport diagnostics
- Transport tracing integration
- Transport integration tests

### Planned Transport Implementations

- TCP/IP transport
- Serial transport
- BLE transport
- MQTT transport

### Planned Discovery Support

- Network discovery
- Serial-device discovery
- Transport-specific endpoint discovery

### Future Protocol Explorer Extensions

- Real transport execution
- Transport diagnostics display
- Optional coloured console output
- Optional Markdown report generation
- Optional HTML report generation

---

# Phase 7 — Gateway

**Status:** Planned

## Objective

Allow HASE endpoints to expose downstream buses and devices.

### Planned

- Gateway endpoints
- I²C forwarding
- SPI forwarding
- Transparent register access
- Downstream-device discovery
- Gateway routing
- Compact downstream-device support

---

# Phase 8 — HASE Studio

**Status:** Planned

## Objective

Provide a complete engineering environment for configuring, operating, observing, and diagnosing HASE systems.

### Planned

- Runtime topology
- Endpoint explorer
- Property editor
- Command execution
- Event monitor
- Trend display
- Protocol tracer
- Transport diagnostics
- Descriptor management
- Connection management
- Simulation integration

---

# Phase 9 — HASE SDK

**Status:** Planned

## Objective

Provide tooling and extension points for third-party instrument and endpoint development.

### Planned

- Instrument templates
- Endpoint templates
- Descriptor editor
- Descriptor validation
- Descriptor repository
- Documentation generation
- Simulation templates
- Transport extension guidance
- Protocol extension guidance
- Example hardware integrations

---

# Long-Term Vision

Future HASE versions may introduce:

- Security and authentication
- Authorization
- Firmware update support
- Additional encoding profiles
- Additional transport profiles
- Distributed runtime services
- Cloud integration
- Descriptor repositories
- Recorded simulation playback
- Advanced gateway routing