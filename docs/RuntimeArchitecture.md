# HASE Runtime Architecture

## Purpose

The runtime represents the live engineering system seen by one application.

It is created from immutable engineering contracts defined in Hase.Core.

---

# Runtime Graph

```
RuntimeContext
    │
    └── RuntimeEndpoint
            │
            └── RuntimeInstrument
                    │
                    ├── RuntimeProperty
                    ├── RuntimeCommand
                    └── RuntimeEvent
```

Each runtime object references its immutable descriptor.

---

# RuntimeContext

The RuntimeContext is the root object for one application.

Responsibilities:

- owns all runtime endpoints
- receives notifications
- provides lookup
- acts as the root of the runtime tree

A RuntimeContext belongs to exactly one application.

---

# RuntimeEndpoint

Represents one engineering endpoint.

Responsibilities:

- references an EndpointDescriptor
- owns RuntimeInstrument objects
- forwards notifications

An endpoint may contain zero or more instruments.

Gateway-only endpoints are therefore supported.

---

# RuntimeInstrument

Represents one instrument.

Responsibilities:

- references an InstrumentDescriptor
- creates runtime objects from the InstrumentInterface
- updates property values
- forwards notifications

The RuntimeInstrument is constructed automatically from its descriptor.

---

# RuntimeProperty

Represents one live engineering property.

Responsibilities:

- references a PropertyDescriptor
- stores the latest PropertyValue
- notifies observers when the value changes

A RuntimeProperty is initially created without a value.

---

# RuntimeCommand

Represents one executable command.

Currently it references the corresponding CommandDescriptor.

Execution behavior will be added later.

---

# RuntimeEvent

Represents one engineering event.

Currently it references the corresponding EventDescriptor.

Runtime event processing will be added later.

---

# Notification Flow

Property updates propagate through the runtime hierarchy.

```
RuntimeProperty
        │
        ▼
RuntimeInstrument
        │
        ▼
RuntimeEndpoint
        │
        ▼
RuntimeContext
```

Applications may subscribe at any level of the hierarchy.

---

# Runtime Tree

All runtime objects implement IRuntimeNode.

This allows generic traversal of the runtime graph independent of the concrete object types.

---

# Services

The runtime itself performs no discovery or communication.

Those responsibilities belong to services.

Examples:

- Discovery
- Polling
- Recording
- Replay

Services modify the runtime graph but do not modify engineering contracts.


Use your repository’s existing ADR directory and naming style if it differs from `docs/adr`.

---

## Simulation

HASE simulation models physical processes independently of HASE runtime objects.

The simulation subsystem is not a separate HASE runtime mode. It is a source of instrument behavior that can later be exposed through the same runtime interfaces as physical instruments.

The conceptual flow is:

---

ValueGenerator
      |
      v
Simulation
      |
      v
Physical state
      |
      v
Simulated instrument
      |
      v
HASE runtime

---

## Device authority and runtime synchronization

The runtime model is a synchronized representation of device state.

Property values in the runtime may be current, stale, unknown, or unavailable
depending on connection and synchronization status.

Property-write operations are requests to the device. The runtime updates its
authoritative cached representation only after device confirmation or after
receiving the resulting property value.

Commands are correlated runtime-to-device operations.

Events are transient device-originated notifications and are not stored as
current property state.

See ADR-0008.


## Negotiated connection capabilities

Each runtime connection has an effective capability set established during
protocol negotiation.

The capability set is connection-scoped and must be recreated after
reconnection.

Runtime components must not infer support from the endpoint type alone.
Availability of an operation depends on:

1. the negotiated protocol capabilities;
2. the endpoint descriptor;
3. the current connection and endpoint state.

Capabilities describe protocol mechanisms. They do not replace descriptor
metadata.

See ADR-0009.

## Runtime message exchange

Runtime components exchange protocol messages rather than transport packets.

Transport adapters convert protocol messages into transport-specific frames
and reconstruct protocol messages from received frames.

The runtime core therefore remains independent of UART, TCP, BLE, MQTT, or
other transports.

Protocol failures are represented by unsuccessful Responses.

Transport failures are handled separately by the transport layer.

See ADR-0010.

## Runtime lifecycle management

Each RuntimeEndpoint maintains a protocol lifecycle independent of the
underlying transport.

Runtime components shall observe protocol lifecycle changes rather than
transport events.

Property cache validity depends on the lifecycle state.

During Operational the cache contains the best known authoritative endpoint
state.

During Synchronization Lost cached values become stale.

During Resynchronizing cached information is validated, refreshed or reused
before returning to Operational.

Applications should use lifecycle information to determine endpoint
availability and cache validity.

See ADR-0011.

# RuntimeArchitecture.md

Add the following section:

## Endpoint session management

A RuntimeEndpoint operates within an Endpoint Session.

The Endpoint Session is independent of both the transport connection and an
individual protocol connection instance.

The runtime establishes a session only after endpoint identity has been
verified.

Temporary transport loss or protocol resynchronization may preserve the
session when the same endpoint identity is verified after recovery.

If endpoint identity changes, the runtime must:

* terminate the previous session;
* fail outstanding Commands;
* terminate active Streams;
* invalidate subscriptions;
* prevent cached Properties from being assigned to the replacement endpoint;
* establish a new session;
* notify applications of endpoint replacement.

The Property cache belongs to the Endpoint Session.

Cached values may be retained as stale or historical information after a
session ends, but they must not become the active state of another session
without an explicit application-level migration process.

Commands, Events, notifications, Streams, diagnostics, and trace records must
be associated with the session in which they occurred.

Runtime components must distinguish:

* transport availability;
* protocol lifecycle state;
* Endpoint Session identity.

See ADR-0012.

# Roadmap.md

Replace or update the Phase 3 section so that it contains:

## Phase 3 – HASE Protocol

* [x] ADR-0008 – Protocol interaction model
* [x] ADR-0009 – Protocol capability model
* [x] ADR-0010 – Protocol message model
* [x] ADR-0011 – Protocol connection lifecycle
* [x] ADR-0012 – Endpoint Session model
* [ ] ADR-0013 – Protocol framing and transport mapping
* [ ] ADR-0014 – Protocol serialization
* [ ] Protocol implementation
* [ ] Transport implementations

# ProjectStatus.md

Extend the current Phase 3 description with:

The protocol architecture now defines:

* interaction semantics;
* capability negotiation;
* protocol message categories;
* protocol connection lifecycle;
* Endpoint Sessions.

An Endpoint Session binds runtime state to one verified endpoint identity and
is independent of both the transport connection and individual protocol
connection instances.

Temporary reconnect and resynchronization may preserve a session when endpoint
identity remains unchanged. Endpoint replacement creates a new session and
invalidates the previous session's active cache, subscriptions, Commands, and
Streams.

Protocol framing, transport mapping, serialization, security, and
implementation have not yet been defined.
