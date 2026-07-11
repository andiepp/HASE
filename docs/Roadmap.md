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


## Phase 3 – HASE Protocol

### Completed architecture

* [x] ADR-0008 – Protocol interaction model
* [x] ADR-0009 – Protocol capability model
* [x] ADR-0010 – Protocol message model
* [x] ADR-0011 – Protocol connection lifecycle
* [x] ADR-0012 – Endpoint Session model
* [x] ADR-0013 – Protocol Context
* [x] ADR-0014 – Protocol framing and transport mapping
* [x] ADR-0015 – Serialization Model and Encoding Profiles
* [x] Runtime Component Model

### Protocol implementation

* [ ] Create `Hase.Protocol`
* [ ] Define protocol interfaces
* [ ] Implement protocol message model
* [ ] Implement serializer
* [ ] Implement encoding profiles
* [ ] Implement framer
* [ ] Implement protocol context
* [ ] Integrate Endpoint Session
* [ ] Implement protocol lifecycle
* [ ] Implement loopback transport
* [ ] Integrate with simulation
* [ ] Create protocol test suite

### Transport implementations

* [ ] Serial transport
* [ ] TCP transport
* [ ] BLE transport
* [ ] MQTT transport
* [ ] Gateway transport

### Future work

* [ ] Firmware update
* [ ] Security and authentication
* [ ] Discovery protocols
* [ ] Gateway routing
* [ ] Additional encoding profiles
* [ ] Additional transport profiles

