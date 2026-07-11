# HASE Roadmap

## Phase 1 — Foundation

Status: Completed

- Hase.Core
- Hase.Runtime
- Runtime graph
- Runtime notifications
- Discovery abstraction
- Tests
- Architecture documentation
- ADR-0001 through ADR-0006

## Phase 2 — Simulation

Status: Current

- Hase.Simulation
- Simulation host
- Environment model
- Value generators
- Simulated environment sensor
- Simulation integration tests

## Phase 3 — Transport

- In-process communication
- Serial
- TCP/IP
- BLE
- MQTT
- Network discovery

## Phase 4 — Diagnostics

- Message tracer
- Runtime diagnostics
- Transport tracing
- Engineering-level message interpretation

## Phase 5 — Gateway

- Gateway endpoints
- I2C forwarding
- SPI forwarding
- Transparent register access
- Downstream device discovery

## Phase 6 — HASE Studio

- Runtime topology view
- Live property editor
- Command execution
- Event display
- Trends
- Tracer UI
- Descriptor management

## Phase 7 — HASE SDK

- Instrument module templates
- Descriptor editor
- Descriptor validation
- Descriptor repository
- Documentation generation
- Simulation templates

## Simulation

### Completed

- simulation time model;
- simulation host;
- constant value generator;
- periodic value generator;
- sine waveform;
- triangle waveform;
- sawtooth waveform;
- square waveform;
- environment simulation;
- immutable environment state;
- unit-test coverage for the simulation foundation.

### Next

- define the generic boundary between simulated instruments and `Hase.Runtime`;
- implement a simulated multi-value environment sensor;
- expose temperature, relative humidity, and air pressure through the normal HASE runtime model.

### Later

- interpolated periodic waveform;
- non-periodic recorded time-series generator;
- configurable square-wave duty cycle;
- noise, drift, calibration, and quantization;
- simulation time scaling;
- manual stepping and pause/resume;
- JSON scenario configuration;
- shared environments observed by several instruments;
- actuators influencing physical simulation state;
- recording and replay.

## Phase 3: HASE Protocol

- [x] Define protocol interaction model in ADR-0008
- [ ] Define protocol capability model
- [ ] Define protocol message model
- [ ] Define request/response correlation
- [ ] Define property synchronization semantics
- [ ] Define command invocation semantics
- [ ] Define event delivery semantics
- [ ] Define protocol framing and encoding
- [ ] Implement protocol model
- [ ] Implement protocol tests

ADR-0009 defines the protocol capability model, including connection-scoped
negotiation, the distinction between capabilities and descriptor metadata,
capability dependencies, and the minimal negotiation baseline.

Concrete capability identifiers, negotiation messages, framing, and encoding
have not yet been defined or implemented.

- [x] Define protocol interaction model (ADR-0008)
- [x] Define protocol capability model (ADR-0009)
- [x] Define protocol message model (ADR-0010)
- [ ] Define protocol connection lifecycle
- [ ] Define framing
- [ ] Define serialization
- [ ] Implement protocol model

## Phase 3 – HASE Protocol

ADR-0008 – Protocol interaction model

ADR-0009 – Protocol capability model

ADR-0010 – Protocol message model

ADR-0011 – Protocol connection lifecycle

ADR-0012 – Endpoint Session model

ADR-0013 – Protocol framing and transport mapping

ADR-0014 – Protocol serialization

Protocol implementation

Transport implementations

