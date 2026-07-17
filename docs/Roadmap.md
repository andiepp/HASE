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

### Physical hardware

- ESP32 endpoint
- BME280 support
- Physical TCP communication
- Physical descriptor exchange
- Physical property reads
- C-007 automatic-reconnect scenario
- One-second physical connectivity probe
- Three-second probe timeout
- Physical fault detection after ESP32 reset
- Physical cache preservation during connection loss
- Physical descriptor and property resynchronization
- Repeated ESP32 reset recovery
- Recovery when the ESP32 is unavailable at startup
- Clean physical-scenario cancellation with Ctrl+C

### Quality

- 629 automated tests
- Automatic reconnect validated with real ESP32 hardware

## Remaining

### Diagnostics

- Transport tracing
- Runtime transport diagnostics
- Statistics-change notifications when required by a consumer
- Configurable TCP connection-attempt timeout

### Additional transports

- Serial
- BLE
- MQTT (evaluation)

### Discovery

- Network discovery
- Serial discovery

### Physical capabilities

- WriteProperty
- ExecuteCommand
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