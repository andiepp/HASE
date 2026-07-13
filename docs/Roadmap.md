# HASE Roadmap

This roadmap describes the planned evolution of HASE from the core runtime through protocol support, transports, tooling, and the SDK.

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

# Phase 3 — Transport

**Status:** Planned

## Objective

Provide transport-independent communication between HASE runtimes.

### Planned

- Loopback transport
- Serial transport
- TCP/IP transport
- BLE transport
- MQTT transport
- Network discovery
- Automatic reconnect
- Transport diagnostics

---

# Phase 4 — Protocol V1

**Status:** Completed

## Objective

Implement the complete binary HASE protocol.

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
- Boundary condition tests

**Current status:**

- **386 automated tests passing**

Protocol Version 1 is considered feature complete.

---

# Phase 5 — Runtime Integration

**Status:** In Progress

## Objective

Connect Protocol V1 to the runtime and physical transports.

### Runtime

- ✓ Protocol dispatcher
- ✓ Runtime request routing
- ✓ Property providers
- ✓ Command handlers
- Event publication (via future protocol session)
- Runtime service integration

### Endpoint Hosting

- Runtime endpoint host
- Instrument adapters
- Runtime lifecycle
- Connection management

### Integration Testing

- End-to-end protocol tests
- Runtime integration tests
- Transport integration tests
- Hardware integration tests

---

# Phase 6 — Gateway

**Status:** Planned

## Objective

Allow HASE endpoints to expose downstream buses and devices.

### Planned

- Gateway endpoints
- I²C forwarding
- SPI forwarding
- Transparent register access
- Downstream device discovery

---

# Phase 7 — HASE Studio

**Status:** Planned

## Objective

Provide a complete engineering environment.

### Planned

- Runtime topology
- Endpoint explorer
- Property editor
- Command execution
- Event monitor
- Trend display
- Protocol tracer
- Descriptor management

---

# Phase 8 — HASE SDK

**Status:** Planned

## Objective

Provide tooling for third-party instrument development.

### Planned

- Instrument templates
- Descriptor editor
- Descriptor validation
- Descriptor repository
- Documentation generation
- Simulation templates

---

# Long-Term Vision

Future protocol versions may introduce:

- Security and authentication
- Firmware update support
- Gateway routing
- Additional encoding profiles
- Additional transport profiles
- Distributed runtime services
- Cloud integration