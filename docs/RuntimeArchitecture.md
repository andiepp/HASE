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

## Relationship to the runtime component model

This document describes runtime behavior, including:

* connection lifecycle;
* synchronization;
* recovery;
* cache validity;
* endpoint replacement;
* operational availability.

The architectural components that own these responsibilities are defined in
`RuntimeComponentModel.md`.

In particular:

* Transport owns communication;
* Protocol Context owns protocol execution;
* Endpoint Session owns endpoint identity and the active synchronized cache;
* Runtime Endpoint exposes endpoint functionality to applications.

Behavioral descriptions in this document must remain consistent with those
ownership boundaries.

Component ownership and dependencies are defined in
`RuntimeComponentModel.md`.

## Protocol execution ownership

Protocol execution is owned by the Protocol Context.

The Protocol Context coordinates negotiation, lifecycle transitions, request
correlation, timeouts, heartbeat handling, synchronization, and
resynchronization.

The Endpoint Session owns endpoint identity and session-scoped runtime state.

The Runtime Endpoint exposes application-facing endpoint functionality.

This separation prevents RuntimeEndpoint from becoming responsible for
transport, protocol, identity, cache, and application behavior simultaneously.

See ADR-0013 and `RuntimeComponentModel.md`.


## Framing and serialization boundary

The runtime protocol layer operates on complete protocol messages.

Raw transport data is processed below the Protocol Context through framing and
serialization infrastructure.

On receive:

the Transport supplies transport data;
the Framer reconstructs one complete HASE frame;
the Serializer reconstructs one protocol message;
the Protocol Context processes the protocol message;
the Endpoint Session and runtime model validate and apply endpoint-specific
results.

On send, the sequence is reversed.

Partial frames, frame delimiters, transport packets, and transport-level
segments are not exposed to the Protocol Context.

Framing, serialization, transport, and protocol failures remain distinct so
that lifecycle and recovery logic can respond appropriately.

See ADR-0014 and RuntimeComponentModel.md.

## Framing and serialization pipeline

The runtime processes protocol communication through a layered pipeline.

On transmission, the processing sequence is:

```text
Runtime Model
        │
        ▼
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

On reception, the sequence is reversed:

```text
Transport
        │
        ▼
Framer
        │
        ▼
Complete Frame
        │
        ▼
Serializer
        │
        ▼
Protocol Message
        │
        ▼
Protocol Context
        │
        ▼
Endpoint Session
        │
        ▼
Runtime Model
```

The Protocol Context never processes raw transport bytes.

Likewise, the Runtime Model never processes protocol messages directly.

Each architectural layer receives only the abstraction provided by the layer
below it.

### Reception

Incoming transport data may arrive in arbitrary fragments.

Examples include:

* multiple transport reads for one HASE frame;
* several HASE frames received during one transport read;
* delayed transport data;
* transport-specific segmentation.

These transport details are resolved entirely within the framing layer.

Only after one complete HASE frame has been reconstructed is the serialized
message passed to the Serializer.

The Serializer reconstructs exactly one protocol message before forwarding it
to the Protocol Context.

The Protocol Context validates the message against the current protocol
lifecycle and active Endpoint Session.

Only after successful protocol validation may endpoint-specific state be
applied to the Runtime Model.

### Transmission

When the Runtime Model initiates an operation, the Protocol Context constructs
the corresponding protocol message.

The Serializer converts that message into its serialized representation.

The Framer encapsulates the serialized representation into one HASE frame.

The Transport is responsible for delivering the framed data to the peer.

Transport-specific buffering, segmentation, and retransmission remain below
the framing boundary.

### Error handling

Each layer reports only the failures that belong to its own responsibility.

Typical examples include:

**Transport**

* communication failure;
* disconnection;
* write failure;
* native transport errors.

**Framer**

* malformed frame;
* invalid frame length;
* oversized frame;
* framing synchronization failure.

**Serializer**

* malformed serialized representation;
* unsupported serialization version;
* invalid encoded value.

**Protocol Context**

* timeout;
* unsupported protocol operation;
* capability mismatch;
* correlation failure;
* protocol lifecycle transition.

**Runtime Model**

* unavailable endpoint;
* stale Property values;
* rejected application operation.

Failures propagate upward only after they have been translated into the
appropriate architectural abstraction.

This separation allows each layer to evolve independently while maintaining a
clear ownership boundary.

### Recovery

Recovery follows the protocol lifecycle defined in ADR-0011.

Transport recovery begins with restoration of communication.

Framing recovery restores valid frame boundaries.

Serialization resumes after a complete frame has been reconstructed.

The Protocol Context performs protocol recovery, capability validation,
descriptor validation, and resynchronization.

The Endpoint Session validates endpoint identity before allowing cached
runtime state to become authoritative again.

Only after successful recovery does the runtime return to the Operational
state.

### Relationship to the component model

The responsibilities of the individual runtime components are defined in
`RuntimeComponentModel.md`.

This document describes how those components cooperate during normal
operation, error handling, and recovery.

Together, these documents define both the structural and behavioral
architecture of the HASE runtime.


## Serialization and encoding behavior

Protocol execution is independent of the concrete representation used on the
communication channel.

The runtime therefore separates semantic protocol processing from message
representation.

### Outbound processing

When the runtime transmits a protocol operation, processing follows this
sequence:

```text
Runtime Model
        │
        ▼
Protocol Context
        │
        ▼
Protocol Message
        │
        ▼
Serializer
        │
        ▼
Serialization Model
        │
        ▼
Encoding Profile
        │
        ▼
Serialized Message
        │
        ▼
Framer
        │
        ▼
Transport
```

The Protocol Context creates semantic Protocol Messages.

The Serializer transforms those messages into the canonical Serialization
Model and applies the negotiated Encoding Profile.

The Framer encapsulates the encoded Serialized Message into one HASE frame.

The Transport delivers the framed data.

### Inbound processing

Incoming communication follows the reverse sequence:

```text
Transport
        │
        ▼
Framer
        │
        ▼
Serialized Message
        │
        ▼
Encoding Profile
        │
        ▼
Serialization Model
        │
        ▼
Serializer
        │
        ▼
Protocol Message
        │
        ▼
Protocol Context
        │
        ▼
Endpoint Session
        │
        ▼
Runtime Model
```

The Transport supplies communication data.

The Framer reconstructs one complete HASE frame.

The Serializer applies the negotiated Encoding Profile and reconstructs the
canonical Serialization Model before producing one semantic Protocol Message.

Only semantic Protocol Messages are processed by the Protocol Context.

### Encoding Profile selection

The active Encoding Profile is established during capability negotiation.

Exactly one Encoding Profile is active for a protocol connection unless a
future protocol extension explicitly defines otherwise.

The selected profile determines only the encoded representation.

It does not change protocol semantics.

Changing from one Encoding Profile to another shall not change the observable
behavior of a valid Protocol Message.

### Serialization failures

Serialization failures occur before protocol execution.

Typical causes include:

* malformed encoded data;
* unsupported Encoding Profile version;
* invalid field representation;
* missing required fields;
* unsupported mandatory fields;
* values outside the permitted canonical range.

Serialization failures prevent construction of a valid Protocol Message.

Consequently, they are not represented as protocol Responses.

Instead, they are handled by the runtime as communication failures and may
trigger protocol recovery according to the lifecycle defined in ADR-0011.

### Behavioral separation

The runtime intentionally separates:

* protocol semantics;
* canonical message structure;
* encoded representation;
* framing;
* transport communication.

This separation allows:

* multiple Encoding Profiles to represent identical protocol semantics;
* transport-independent protocol execution;
* independent evolution of serialization and framing;
* efficient binary communication for constrained devices;
* human-readable communication for diagnostics and tooling.

Applications interact only with the Runtime Model.

They remain independent of the Encoding Profile selected for a connection.

### Relationship to the architecture

The responsibilities of the Serializer are defined in
`RuntimeComponentModel.md`.

The protocol representation rules are defined by ADR-0015.

This document describes how serialization participates in the runtime
behavior during normal communication, recovery, and reconnect.



