# HASE Roadmap

## Vision

HASE is built in layers.

Each completed phase becomes a stable foundation for the following phases.
Architecture changes should become increasingly rare as the framework matures.

---

# Phase 1 — Foundation

**Status:** ✅ Completed

Implemented

- Core domain model
- Identity model
- Descriptor model
- Runtime model
- Runtime notifications
- Architecture documentation
- ADR-0001 … ADR-0008
- Comprehensive unit tests

---

# Phase 2 — Simulation

**Status:** ✅ Completed

Implemented

- Hase.Simulation
- Simulation host
- Environment simulation
- Value generators
- Runtime integration
- Simulation tests

Future extensions

- Noise models
- Calibration models
- Playback
- JSON scenarios

---

# Phase 3 — Protocol Foundation

**Status:** ✅ Completed

Implemented

- Protocol architecture
- Binary serialization
- Envelope framing
- Serialization helpers
- Variant serialization
- Property value serialization
- Boolean data descriptors

---

# Phase 4 — Protocol Version 1

**Status:** ✅ Completed

Implemented

- Discover
- Endpoint descriptors
- Property read
- Property write
- Command execution
- Events
- String data-descriptor encoding
- Numeric data-descriptor encoding
- Boolean data-descriptor encoding with discriminator `0x03`

Protocol Version 1 is feature complete.

---

# Phase 5 — Runtime Integration

**Status:** ✅ Completed

Implemented

- Runtime dispatcher
- Runtime routing
- Protocol Explorer
- Capability scenarios
- Loopback transport
- Runtime integration

Completion baseline

- 428 automated tests

---

# Phase 6 — Transport Infrastructure

**Status:** 🚧 In Progress

## Completed

### Transport

- Transport abstraction
- Loopback transport
- TCP transport
- Framed communication
- Transport manager
- Connection lifecycle
- Connection health
- Connection notifications
- Configurable TCP connection-attempt timeout
- Five-second default TCP connection timeout
- Explicit TCP timeout configuration
- Infinite operating-system timeout option
- Caller-cancellation preservation
- `TimeoutException` for connection-attempt timeout
- Deterministic TCP timeout tests

### Runtime synchronization

- Runtime endpoint coordinator
- Endpoint synchronizer
- Descriptor synchronization
- Descriptor compatibility validation
- Readable-property synchronization
- Runtime cache population
- Runtime property notifications
- Successful integration tests
- Failure integration tests

### Automatic connection recovery

- `RuntimeEndpointConnectionSupervisor`
- `IRuntimeEndpointReconnectPolicy`
- Default retry policy
- Immediate first retry
- Retry delays of 1 second, 2 seconds, and 5 seconds
- Maximum retry delay of 10 seconds
- Automatic retry after initial connection failure
- Automatic transport replacement after connection fault
- Full descriptor resynchronization after reconnect
- Full readable-property resynchronization after reconnect
- Synchronization retry over an already-connected transport
- Retry-attempt reset after successful recovery
- Cached-property preservation while disconnected
- Single supervision task per supervisor
- Cancellation ending in `Disconnected`
- Automatic-reconnect integration tests

### Connection and recovery statistics

- Immutable `RuntimeEndpointConnectionStatistics` snapshots
- Thread-safe statistics access
- Initial connection-attempt count
- Initial connection-failure count
- Reconnect-attempt count
- Reconnect-failure count
- Successful-recovery count
- Last recovery start time in UTC
- Last successful recovery completion time in UTC
- Monotonic total recovery duration
- `.NET TimeProvider` integration
- Deterministic recovery-timing tests
- Recovery-cancellation statistics semantics

### Transport exchange tracing

- Transport-independent `TransportExchangeTrace`
- Explicit succeeded, failed, and cancelled outcomes
- Optional trace-source and trace-observer interfaces
- Thread-safe observer subscription and publication
- Observer-failure isolation
- Per-connection exchange sequence numbers
- UTC exchange timestamps
- Monotonic exchange-duration measurement
- Request and response byte counts
- Transport state at exchange completion
- Exception type and message for failed and cancelled exchanges
- No raw request or response payload capture
- Successful, failed, and cancelled TCP exchange tests

### Runtime transport diagnostics

- Immutable `TransportExchangeStatistics` snapshots
- Successful, failed, cancelled, and completed exchange counts
- Aggregate request and response byte counts
- Aggregate monotonic exchange duration
- Most recent exchange completion and outcome
- Thread-safe `TransportExchangeStatisticsCollector`
- Immutable snapshot access during concurrent trace publication
- Automatic collection from trace-capable manager connections
- Optional-capability compatibility for connections without tracing
- Collector detachment from replaced and disposed connections
- Aggregate statistics preserved across connection replacement
- Immutable `RuntimeEndpointConnectionDiagnostics`
- Combined transport-health snapshot
- Combined connection and recovery statistics
- Combined transport-exchange statistics
- `RuntimeEndpointConnectionSupervisor.GetDiagnostics()` extension
- Connected-state diagnostic composition tests
- Recovery diagnostic composition tests
- C-007 aggregate diagnostic output

### Boolean descriptor architecture extension

- `BooleanDataDescriptor`
- Protocol Version 1 Boolean descriptor discriminator `0x03`
- Boolean descriptor serialization and deserialization
- Boolean descriptor round-trip tests
- Boolean `ReadWrite` property-descriptor round trip
- Existing String and Numeric discriminator preservation
- Matching ESP32 Boolean descriptor encoding

### Physical endpoint

- DOIT ESP32 DEVKITC V4 endpoint
- Endpoint ID `doit-esp32-devkitc-v4-01`
- BME280 environment-sensor instrument
- ESP32 GPIO controller instrument
- Physical TCP communication
- Physical discovery of both instruments
- Strict physical descriptor validation
- Physical temperature reads
- Physical relative-humidity reads
- Physical air-pressure reads
- Physical Boolean status-LED reads
- GPIO16 active-low status LED
- Initial LED value `false`
- Initial GPIO16 state high
- Initial LED state off
- Physical command descriptor discovery
- `Controller.ToggleStatusLed` command

### Physical WriteProperty

- `physical.controller.status-led-enabled`
- Boolean `ReadWrite` access
- ESP32 Boolean request decoding
- Strict Boolean variant validation
- ESP32 WriteProperty message dispatch
- Physical property-target validation
- GPIO16 active-low state application
- WriteProperty success-response serialization
- Applied Boolean value returned in successful responses
- Truthful UTC timestamps
- `PropertyQuality.Good`
- Unknown-property `NotFound` responses
- Invalid-value-type `InvalidRequest` responses
- Rejected writes leave hardware state unchanged
- Physical write-then-read verification
- Final scenario state is LED off

### Physical ExecuteCommand

- `Controller.ToggleStatusLed`
- Null command argument contract
- Boolean new-state return value
- ESP32 ExecuteCommand request decoding
- Strict null-variant validation
- ESP32 ExecuteCommand message dispatch
- Physical instrument and command-path validation
- GPIO16 active-low toggle execution
- ExecuteCommand success-response serialization
- Shared authoritative state for property and command operations
- Unknown-command `NotFound` responses
- Non-null-argument `InvalidRequest` responses
- Rejected commands leave hardware state unchanged
- Command return and property-readback verification
- Final scenario state is LED off

### Physical capability scenarios

- C-003 physical TCP connectivity
- C-004 physical discovery
- C-005 strict complete descriptor validation
- C-006 physical BME280 property reads
- C-007 automatic reconnect, tracing, and diagnostics
- C-008 physical Boolean WriteProperty and read-back
- C-009 rejected physical WriteProperty validation
- C-010 physical ExecuteCommand and property read-back
- C-011 rejected physical ExecuteCommand validation

### Physical validation

- Automatic reconnect validated after ESP32 reset
- Descriptor and property resynchronization
- Cached-value preservation during connection loss
- TCP connection timeout validation
- Transport exchange tracing across replacement
- Runtime transport diagnostics across replacement
- Full endpoint rename validation
- Two-instrument discovery validation
- GPIO16 active-low electrical behavior
- Boolean write `false → true → false`
- Boolean read-back after each write
- Unknown-property rejection
- Invalid-value-type rejection
- Unchanged state after rejected writes
- Command toggle `false → true → false`
- Boolean command return after each toggle
- Property read-back after each command
- Unknown-command rejection
- Non-null-command-argument rejection
- Unchanged state after rejected commands

### Quality

- 714 automated tests
- Automatic reconnect validated with real ESP32 hardware
- TCP connection timeout validated with real ESP32 hardware
- Transport exchange tracing validated with real ESP32 hardware
- Runtime transport diagnostics validated with real ESP32 hardware
- Physical Boolean WriteProperty validated with real ESP32 hardware
- Physical ExecuteCommand validated with real ESP32 hardware

## Remaining

### Diagnostics

- Statistics-change notifications when required by a consumer

### Additional transports

- Serial
- BLE
- MQTT evaluation

### Discovery

- Network discovery
- Serial discovery

### Physical capabilities

- EventNotification

---

# Phase 7 — Gateway

**Status:** Planned

Goals

- I²C gateway
- SPI gateway
- Downstream discovery
- Compact descriptors

---

# Phase 8 — HASE Studio

**Status:** Planned

Goals

- Topology view
- Endpoint explorer
- Property editor
- Command execution
- Event monitor
- Trending
- Protocol tracer
- Diagnostics
- Descriptor management
- Simulation integration

---

# Phase 9 — HASE SDK

**Status:** Planned

Goals

- Instrument templates
- Endpoint templates
- Descriptor tools
- Validation
- Documentation generation
- Example projects

---

# Long-term

Possible future directions

- Authentication
- Authorization
- Firmware update
- Cloud integration
- Descriptor repositories
- Distributed runtime services
- Additional protocol profiles
- Additional transport profiles