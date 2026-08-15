# HASE — Hardware Access System Environment

HASE is a descriptor-driven hardware access platform that exposes heterogeneous
physical and simulated devices through a consistent runtime model. Hardware
capabilities are represented as Properties, Commands, and Events, allowing
applications to interact with network, USB-serial, and simulated endpoints
without depending on device-specific transport details.

The current validated system connects a Windows desktop runtime host to an
ESP32 over a local IP network and an Arduino Uno over USB serial. An
authenticated laptop client reaches that runtime host through a private
network, discovers the published hardware model, reads and writes Properties,
executes Commands, receives Events, and follows endpoint connection-state
changes.

## Current capabilities

- Descriptor-driven endpoints, instruments, Properties, Commands, and Events
- Native HASE Protocol Version 1 over framed TCP
- Compact Serial Protocol Version 1 for resource-constrained endpoints
- Physical, simulated, network, and USB-serial endpoint support
- Explicit endpoint discovery, verification, attachment, and detachment
- Endpoint-authoritative identity and attachment generations
- Runtime-owned connection supervision and automatic recovery
- Cached Property values with timestamps and quality
- Authoritative Property reads and writes
- Command execution without implicit retry
- Transient Event observation without replay
- Normalized northbound snapshot, Property, Command, and observation services
- Versioned gRPC API over HTTP/2
- Mutual-TLS client authentication and certificate authorization
- Reusable transport-independent .NET client contracts
- Recovering gRPC client sessions
- Asyncio-native Python Client with reproducible local wheel distribution,
  isolated installed automation, and explicit Desktop/MiniPC target selection
- WPF laptop client with:
  - Runtime-host identity and API-version display
  - Endpoint and instrument inventory
  - Live Property values
  - Descriptor-driven Boolean, Numeric, String, and ByteArray Property writes
  - Parameterless Command execution
  - Endpoint disconnect and reconnect state
  - Live Event feed with endpoint and instrument attribution
  - Separate bounded diagnostics window for client lifecycle and structured
    northbound activity
  - Explicitly authorized, profile-scoped projection of bounded Runtime Host
    Operational, Protocol, and Bytes diagnostics
  - Exact projected byte summaries and hexadecimal content with Host timestamp,
    endpoint scope, original length, captured length, and truncation state
  - Presentation Pause/Resume without interrupting capture or client operation
  - Independent live-only diagnostic subscriptions without mutation replay

## Validated hardware

### DOIT ESP32 DevKitC V4

The native Protocol Version 1 validation endpoint exposes:

- A BME280 environment sensor
  - Temperature
  - Relative humidity
  - Air pressure
- A GPIO controller
  - Status LED Property and Command
  - GPIO17 push-button Event
- Network connectivity over Wi-Fi and framed TCP
- Disconnect, reset, Wi-Fi-loss, and reconnect recovery

### Arduino Uno

The Compact Serial Protocol Version 1 validation endpoint exposes:

- Built-in LED state as a Boolean Property
- LED toggle as a Command
- Push-button notifications as an Event
- USB-serial discovery and authoritative endpoint verification
- USB unplug, reset, and reconnect recovery

These devices validate the architecture; they do not define its limits. HASE's
runtime model supports other endpoint families and multi-instrument devices.

## System overview

```text
 ESP32 / Arduino / Simulation
              │
              ▼
         HASE Runtime Host
 attachment · supervision · recovery
              │
              ▼
      Authenticated gRPC API
              │
              ▼
   .NET SDK · WPF Client · Automation
```

The runtime host owns physical endpoint connections and publishes a normalized
hardware model northbound. Remote clients do not connect directly to devices.
They address operations using endpoint identity, attachment generation,
instrument identity, and descriptor path.

The attachment generation prevents a client from applying an operation prepared
for an earlier physical attachment to a later replacement or reconnection.
Transient Events are delivered live and are not replayed to later subscribers.

## Repository structure

| Location | Purpose |
| --- | --- |
| `src/Hase.Core` | Descriptor, identity, data, and endpoint domain model |
| `src/Hase.Protocol` | Native HASE Protocol Version 1 |
| `src/Hase.CompactProtocol` | Compact Serial Protocol Version 1 |
| `src/Hase.Transport` | Transport abstractions and framed connections |
| `src/Hase.Runtime` | Runtime endpoint graph and interaction model |
| `src/Hase.Runtime.Transport` | Physical transport integration, attachment, and supervision |
| `src/Hase.Runtime.Northbound` | Normalized runtime-host API boundary |
| `src/Hase.Runtime.Remote.Grpc.*` | Versioned gRPC contracts, mapping, and hosting |
| `src/Hase.Client` | Transport-independent client contracts and state |
| `src/Hase.Client.Grpc` | Authenticated and recovering gRPC client implementation |
| `src/Hase.Client.Wpf` | WPF laptop-client presentation and view models |
| `src/Hase.Client.Wpf.App` | Executable WPF application shell |
| `src/Hase.Simulation*` | Simulation models and runtime adapters |
| `src/HASE.ProtocolExplorer` | Capability and physical-validation scenarios |
| `tests` | Unit, integration, transport, remote API, and client tests |
| `docs` | Architecture, decisions, capability reports, API reference, and tutorials |

## Getting started

### Prerequisites

- .NET 10 SDK
- Windows for the WPF application and current Windows USB discovery path
- Appropriate hardware, firmware, and network access for physical validation
- Runtime-host and client certificates for authenticated remote operation

The core architecture and transport abstractions are not intended to be
Windows-specific. Platform-specific discovery and desktop UI support are
separate concerns.

### Build

From the repository root:

```powershell
dotnet build .\HASE.slnx
```

### Test

```powershell
dotnet test .\HASE.slnx
```

Physical hardware validations are intentionally separate from ordinary
automated tests. They require explicit endpoint selection and do not
automatically attach arbitrary discovered devices.

### Author an ESP32 endpoint

Use the
[HASE ESP32 Endpoint Authoring Guide](docs/ESP32-Endpoint-Authoring-Guide.md)
to create or adapt a Protocol Version 1 ESP32 application. It explains the five
tracked application files, local ignored Wi-Fi secrets, typed capability
registration, hardware callbacks, Event publication, and controlled
compilation without firmware upload.

ADR-0054 also defines the separate controlled physical-deployment path:
read-only preflight, sensitive Current and Rollback bundle preparation, a bound
readiness plan, explicit confirmation, one upload without automatic retry or
rollback, and Runtime Host/Client validation. Upload from an isolated workspace
because Arduino CLI may create additional `_flashed.bin` files beside its
inputs; never treat a successful compilation as upload authorization.

### Run the laptop client

Start with the
[HASE Laptop Client UI Tutorial](docs/Tutorial/HASE-Laptop-Client-UI-Tutorial.md).
It covers publishing the desktop runtime host and WPF application, preparing
the laptop configuration, connecting through the authenticated northbound API,
and exercising the exposed Properties, Commands, and Events.

For certificate and configuration preparation, see
[Private Network Credential Provisioning](docs/Private-Network-Credential-Provisioning.md).
Do not commit private keys, certificate passwords, private network addresses,
or environment-specific client configuration files.

## Documentation

- [ESP32 Endpoint Authoring Guide](docs/ESP32-Endpoint-Authoring-Guide.md)
- [ADR-0054 — ESP32 Endpoint Library and Application Authoring Boundary](docs/adr/ADR-0054-ESP32-Endpoint-Library-and-Application-Authoring-Boundary.md)
- [Northbound API Reference](docs/API%20reference/HASE-Northbound-API-Reference.md)
- [Laptop Client UI Tutorial](docs/Tutorial/HASE-Laptop-Client-UI-Tutorial.md)
- [Descriptor-Driven Property Editing Tutorial](docs/Tutorial/HASE-Descriptor-Driven-Property-Editing-Tutorial.md)
- [Architecture](docs/Architecture.md)
- [Runtime Architecture](docs/RuntimeArchitecture.md)
- [Runtime Component Model](docs/RuntimeComponentModel.md)
- [Serialization Model](docs/SerializationModel.md)
- [Project Status](docs/ProjectStatus.md)
- [Roadmap](docs/Roadmap.md)
- [Architecture Decision Records](docs/adr)
- [Python Client](python/hase-client/README.md)
- [ADR-0051 — Python Client Local Distribution and Automation Workflows](docs/adr/ADR-0051-Python-Client-Local-Distribution-and-Automation-Workflows.md)

The capability reports under `docs` record focused implementation and physical
validation milestones. The ADR collection records the architectural decisions
behind the current design.

## Architecture principles

- **Descriptor-driven model:** interfaces are described independently of their
  transport implementation.
- **Endpoint-authoritative identity:** discovery metadata identifies candidates;
  the protocol-confirmed endpoint identity is authoritative.
- **Explicit attachment:** discovery and verification never imply automatic
  attachment.
- **No automatic replacement:** endpoint replacement requires an explicit
  decision.
- **Runtime-owned lifecycle:** the runtime host owns physical connections,
  synchronization, health probing, and reconnection.
- **Generation-qualified operations:** remote operations target a specific
  published attachment generation.
- **Device-authoritative state:** Property writes are confirmed by the endpoint;
  Command execution does not imply a Property-cache update.
- **Transient Events:** Events are pushed live and are not retained for replay.
- **Secure northbound boundary:** remote access uses mutual TLS and an explicit
  client authorization policy.
- **Transport-independent clients:** application-facing client contracts do not
  expose gRPC implementation details.

## Project status

HASE is under active development. The current end-to-end scenario has been
physically validated with:

- A desktop runtime host managing an ESP32 and Arduino Uno concurrently
- A Windows laptop connecting through a private network
- Mutual-TLS client authentication
- Live endpoint inventory and connection-state recovery
- Property reads and writes
- Command execution
- Correctly attributed Arduino and ESP32 push-button Events

See [Project Status](docs/ProjectStatus.md) for the maintained implementation
status and [Roadmap](docs/Roadmap.md) for planned work.
