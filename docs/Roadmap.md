# HASE Roadmap

This roadmap describes the planned evolution of HASE from the core runtime
through protocol support, transport infrastructure, gateways, engineering
tooling, and the SDK.

---

# Phase 1 — Foundation

**Status:** Completed

## Objective

Establish the core domain model and runtime architecture.

## Completed

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

Provide a complete simulation environment for developing and testing HASE
without physical hardware.

## Completed

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

## Future Extensions

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

Define the HASE protocol architecture and establish its binary serialization
foundation.

## Completed

- Protocol message model
- Protocol request and response abstractions
- Protocol message types
- Binary protocol reader
- Binary protocol writer
- Binary payload encoding
- Protocol envelope encoding
- Protocol serialization helpers
- Descriptor-path serialization
- Protocol error handling
- Protocol architecture documentation

---

# Phase 4 — Protocol Implementation

**Status:** Completed

## Objective

Implement Protocol Version 1 for discovery, descriptors, properties, commands,
and events.

## Protocol Infrastructure

- BinaryProtocolReader
- BinaryProtocolWriter
- BinaryProtocolPayloadCodec
- ProtocolEnvelopeByteCodec
- ProtocolSerializationHelper

## Descriptor Serialization

- EndpointDescriptorSerializer
- EndpointMetadataSerializer
- InstrumentDescriptorSerializer
- InstrumentMetadataSerializer
- InstrumentInterfaceSerializer
- PropertyDescriptorSerializer
- CommandDescriptorSerializer
- EventDescriptorSerializer
- DataDescriptorSerializer

## Runtime Serialization

- VariantSerializer
- PropertyValueSerializer

## Implemented Messages

### Discovery

- DiscoverRequest
- DiscoverResponse

### Descriptor Access

- ReadEndpointDescriptorRequest
- ReadEndpointDescriptorResponse

### Property Access

- ReadPropertyRequest
- ReadPropertyResponse
- WritePropertyRequest
- WritePropertyResponse

### Command Execution

- ExecuteCommandRequest
- ExecuteCommandResponse

### Event Distribution

- EventNotification

## Testing

- Binary protocol verification
- Round-trip serialization tests
- Error-handling tests
- Boundary-condition tests

Protocol Version 1 is feature complete for the currently defined message set.

---

# Phase 5 — Runtime Integration and Protocol Explorer

**Status:** Completed

## Objective

Connect Protocol Version 1 to the runtime, demonstrate runtime capabilities,
and establish byte-oriented protocol exploration and tracing.

## Runtime Integration

- Protocol dispatcher
- Runtime request routing
- Property providers
- Property read and write routing
- Command handlers
- Runtime service integration
- Runtime endpoint hosting foundations
- Runtime instrument integration
- End-to-end protocol-dispatch tests

## Capability Demonstrations

- C-001 property capability demonstration
- C-002 command capability demonstration
- Shared capability-scenario framework
- Scenario runner
- Protocol scenario base

## Protocol Explorer

- Protocol message visualization
- Request and response visualization
- Annotated payload visualization
- Protocol scenario execution
- Runtime capability demonstrations
- Byte-oriented loopback execution

## Transport Foundation

- Hase.Transport project
- ITransportConnection
- Protocol-independent byte exchange
- LoopbackTransportConnection
- Loopback transport tests
- Separation of protocol, runtime, and transport layers

## Testing

- Runtime protocol-dispatch tests
- Capability-scenario tests
- Protocol Explorer integration tests
- Loopback transport contract tests

## Completion Baseline

- **428 automated tests passing**

---

# Phase 6 — Transport Infrastructure and Physical Endpoint Integration

**Status:** In Progress

## Objective

Provide production-ready, protocol-independent communication infrastructure
between HASE runtimes and endpoints, and validate it against real hardware.

The transport layer exchanges byte sequences and remains independent of the
HASE protocol and runtime models.

## Completed Transport Foundations

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
- TCP transport connection
- TCP transport factory
- TCP transport options
- Framed TCP communication
- Payload-size validation
- Physical transport execution from Protocol Explorer

## Completed Physical Endpoint Integration

- ESP32 endpoint application
- DOIT ESP32 DEVKIT V4 target
- Wi-Fi connection
- TCP server on port `5000`
- Protocol envelope decoding
- Request dispatch
- DiscoverRequest handling
- DiscoverResponse generation
- ReadEndpointDescriptorRequest handling
- ReadEndpointDescriptorResponse generation
- Physical endpoint descriptor
- Physical environment-sensor descriptor
- BME280 hardware abstraction
- BME280 initialization through I²C
- GPIO21 SDA
- GPIO22 SCL
- BME280 I²C address `0x76`

## Completed Physical Property Infrastructure

- ReadPropertyRequest recognition
- ReadPropertyRequest decoding
- Instrument validation
- Property validation
- Physical property-service abstraction
- Table-driven property lookup
- ReadPropertyResponse serialization
- Success responses
- Invalid-request responses
- Not-found responses
- Internal-error responses
- `double` variant serialization
- Signed 64-bit timestamp serialization
- UTC synchronization through SNTP
- Unix timestamp generation in milliseconds
- `PropertyQuality.Good`

## Completed Physical Properties

The ESP32 BME280 endpoint exposes:

- `physical.environment-sensor.temperature`
- `physical.environment-sensor.relative-humidity`
- `physical.environment-sensor.air-pressure`

## Completed Physical Protocol Explorer Scenarios

### C-003

Physical TCP connectivity.

### C-004

Physical endpoint discovery.

### C-005

Physical endpoint-descriptor request and validation.

### C-006

Complete live property request/response validation for:

- Temperature
- Relative humidity
- Air pressure

C-006 validates:

- Framed TCP exchange
- Protocol version
- Request and response roles
- Message types
- Correlation identifiers
- Protocol result
- Optional property-value presence
- `double` variant type
- Engineering value
- Plausible sensor range
- UTC timestamp
- Property quality
- Round-trip time

## Remaining Transport Infrastructure

- Shared transport contract testing
- Explicit transport lifecycle semantics
- Connection-state model
- Transport configuration consolidation
- Transport creation and selection
- Connection management
- Automatic reconnect
- Endpoint reinitialization after reconnect
- Cancellation and timeout policy
- Transport diagnostics
- Transport tracing integration
- Transport integration tests
- Physical connection-loss tests

## Remaining Transport Implementations

- Serial transport
- BLE transport
- MQTT transport evaluation or implementation

## Remaining Discovery Support

- Network discovery
- Serial-device discovery
- Transport-specific endpoint discovery

## Remaining Physical Protocol Capabilities

- Negative-path ReadProperty tests
- Physical WriteProperty
- Physical ExecuteCommand
- Physical EventNotification
- Hardware-unavailable handling tests
- Invalid-request handling tests
- Unknown-instrument handling tests
- Unknown-property handling tests

## Future Protocol Explorer Extensions

- Connection-state display
- Transport diagnostics display
- Reconnect visualization
- Optional coloured console output
- Optional Markdown report generation
- Optional HTML report generation

## Phase 6 Completion Criteria

Phase 6 will be complete when:

- TCP lifecycle semantics are defined and tested.
- Automatic reconnect is implemented and validated.
- Connection diagnostics and tracing are available.
- Shared transport contracts are applied to production transports.
- At least TCP and serial transports are operational.
- Discovery is available for the supported physical transports.
- Physical property, command, write, and event paths are validated.
- Connection-loss and recovery behavior is verified end to end.

---

# Phase 7 — Gateway

**Status:** Planned

## Objective

Allow HASE endpoints to expose downstream buses and devices.

## Planned

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

Provide a complete engineering environment for configuring, operating,
observing, and diagnosing HASE systems.

## Planned

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

Provide tooling and extension points for third-party instrument and endpoint
development.

## Planned

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