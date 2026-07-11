# HASE Architecture

## Purpose

HASE (Hardware Abstraction System for Engineering) is a platform for building engineering applications around self-describing hardware.

The primary design goal is to separate the engineering model from runtime execution, communication transports and user interface concerns.

---

# Layered Architecture

HASE is organized into independent layers.

```
Application
        │
        ▼
Services
        │
        ▼
Runtime
        │
        ▼
Core
```

Each layer depends only on the layer below it.

---

# Hase.Core

Hase.Core defines the immutable engineering model.

It contains no runtime state and no communication logic.

Examples:

- Endpoints
- Instruments
- Properties
- Commands
- Events
- Data descriptors
- Quantities
- Units

Objects in Hase.Core are engineering contracts.

---

# Hase.Runtime

Hase.Runtime creates a live runtime graph from engineering contracts.

Runtime objects are mutable.

They represent the current application view of an engineering system.

The runtime graph mirrors the engineering model.

---

# Services

Services perform work on the runtime graph.

Examples include:

- Discovery
- Polling
- Recording
- Replay
- Diagnostics

Services modify the runtime graph but never the engineering contracts.

---

# Future Layers

The current architecture anticipates additional layers.

Examples:

- Hase.Transport
- Hase.Gateway
- Hase.Diagnostics
- Hase.Studio
- Hase.SDK

These layers are intentionally independent from Hase.Core.

---

# Design Principles

## Immutable engineering contracts

Engineering contracts are immutable.

A contract describes what a system is capable of.

---

## Mutable runtime

Runtime objects represent the current live state of an engineering system.

---

## Separation of concerns

Engineering contracts never contain:

- communication
- transport
- polling
- user interface
- runtime state

---

## Transport independence

The engineering model is independent of Serial, TCP/IP, BLE, MQTT or future transports.

---

## Descriptor-driven runtime

The runtime graph is automatically constructed from engineering descriptors.

---

## Services operate on the runtime

Behavior is implemented by services.

Runtime objects remain lightweight representations of the engineering system.

### Simulation independence

Physical-process simulations are independent of HASE runtime objects, 
descriptors, transports, and protocols.

Simulated instruments adapt physical simulation state to the normal HASE runtime model. 
Applications should therefore interact with physical and simulated instruments 
through the same interfaces.

## Protocol message model

Protocol communication consists of immutable protocol messages.

The protocol defines four semantic message categories:

- Request
- Response
- Notification
- Stream

Responses complete Requests.

Notifications are asynchronous.

Streams represent large or sequential data transfers.

The message model is independent of transport, framing, and serialization.

See ADR-0010.

## Protocol connection lifecycle

The HASE protocol defines a transport-independent connection lifecycle.

Transport connectivity alone does not imply protocol readiness.

A protocol connection progresses through the following states:

- Disconnected
- Transport Connected
- Protocol Negotiation
- Capability Negotiation
- Descriptor Discovery
- Initial Synchronization
- Operational
- Synchronization Lost
- Resynchronizing

Only the Operational state guarantees that negotiated protocol mechanisms are
fully available.

The runtime maintains protocol state independently of transport state.

Following synchronization loss, the runtime performs protocol
resynchronization. Previously validated information may be reused when
verification confirms that it remains valid.

See ADR-0011.

## Endpoint sessions

HASE distinguishes between a transport connection, a protocol connection, and
an Endpoint Session.

* A transport connection provides a communication path.
* A protocol connection establishes compatible HASE communication.
* An Endpoint Session binds the runtime to one verified endpoint identity.

An Endpoint Session may continue across temporary transport disconnections,
protocol reconnects, endpoint reboots, and resynchronization, provided that
the returning endpoint identity is verified as unchanged.

A new Endpoint Session is required when another endpoint is detected, even
when the transport address, COM port, endpoint type, or descriptor remains the
same.

Capabilities, descriptors, cached Properties, subscriptions, Commands,
Events, Streams, diagnostics, and application associations are interpreted
within the context of one Endpoint Session.

Runtime state from one session must not be silently assigned to another
session.

Endpoint identity is separate from an internal runtime Session Identifier.
Endpoint identity identifies the endpoint, while the Session Identifier
distinguishes one runtime relationship instance.

See ADR-0012.

## Runtime component model

The HASE runtime separates communication, protocol execution, endpoint
identity, and synchronized runtime state.

The primary architectural responsibilities are:

* Transport owns communication.
* Protocol Context owns protocol execution.
* Endpoint Session owns the verified relationship with one endpoint identity.
* Runtime Endpoint exposes endpoint-level functionality to applications.
* Runtime Instrument groups related Properties, Commands, and Events.
* Runtime Cache stores the synchronized representation of device-owned state.

The device remains authoritative.

The Runtime Cache belongs to one Endpoint Session and must not be silently
reassigned to a replacement endpoint.

The complete responsibility and ownership model is documented in
`RuntimeComponentModel.md`.

## Protocol Context

The Protocol Context is the architectural owner of HASE protocol execution.

It coordinates:

* protocol lifecycle;
* protocol-version negotiation;
* capability negotiation;
* message correlation;
* protocol timers;
* heartbeat handling;
* synchronization;
* Notifications;
* Streams;
* protocol recovery;
* protocol diagnostics.

The Protocol Context depends on a Transport abstraction but remains independent
of endpoint identity and application-visible runtime state.

Endpoint identity and active runtime cache ownership belong to the Endpoint
Session.

See ADR-0013 and `RuntimeComponentModel.md`.


## Protocol framing and transport mapping

The HASE protocol separates protocol semantics from transport mechanics.

Communication passes through distinct architectural layers:

```text
Protocol Context
        │
        ▼
Protocol Message
        │
        ▼
Serializer
        │
        ▼
Serialized Message
        │
        ▼
Framer
        │
        ▼
Frame
        │
        ▼
Transport
```

Each architectural layer has exactly one primary responsibility.

The Protocol Context operates exclusively on protocol messages and is
independent of transport communication.

The Serializer converts protocol messages into serialized representations and
reconstructs protocol messages from serialized representations.

The Framer defines the boundaries of serialized messages while they are
transported.

The Transport moves framed data between communication peers without
interpreting protocol semantics.

Each HASE frame contains exactly one complete serialized protocol message.

A serialized protocol message shall not span multiple HASE frames.

Large or sequential transfers are represented through protocol Streams.

Each Stream message is serialized and framed independently using the same
message pipeline as every other protocol message.

Transport implementations may internally divide one HASE frame into multiple
native transport units.

Such segmentation remains entirely below the HASE framing layer.

The Framer reconstructs the complete HASE frame before deserialization
begins.

Neither the Serializer nor the Protocol Context observes transport fragments.

Framing and serialization are intentionally independent architectural
concepts.

A future serialization format may be introduced without changing framing.

Likewise, a new framing mechanism may be introduced without changing protocol
messages or serialization.

This separation allows HASE to support stream-oriented, packet-oriented,
message-oriented, simulated, and future transports while preserving one
common protocol architecture.

See:

* ADR-0010 – Protocol Message Model
* ADR-0013 – Protocol Context
* ADR-0014 – Protocol Framing and Transport Mapping


