# HASE Runtime Component Model

## Purpose

This document defines the architectural building blocks of the HASE runtime.

It describes:

* the responsibility of each runtime component;
* the ownership boundaries between components;
* the dependencies between components;
* the expected lifetime of component state.

This document defines architectural responsibilities rather than concrete
implementation classes.

An implementation may represent one architectural component using one class,
multiple collaborating classes, immutable records, services, actors, or
another suitable design.

The component model complements:

* `Architecture.md`, which describes the high-level HASE architecture;
* `RuntimeArchitecture.md`, which describes runtime behavior;
* the Architecture Decision Records, which document individual design
  decisions.

## Design principles

The runtime component model follows these principles:

* each component has one primary responsibility;
* transport concerns remain separate from protocol concerns;
* protocol execution remains separate from endpoint identity;
* endpoint identity remains separate from synchronized runtime state;
* the device remains authoritative;
* the runtime maintains a synchronized cache;
* cached state belongs to one verified Endpoint Session;
* applications interact with runtime models rather than transport or protocol
  details;
* components depend on abstractions rather than transport-specific
  implementations.

## Component overview

The primary runtime components are:

* Transport
* Protocol Context
* Endpoint Session
* Runtime Endpoint
* Runtime Instrument
* Runtime Property
* Runtime Command
* Runtime Event
* Runtime Cache

Their conceptual relationship is:

```text
Application
    │
    ▼
Runtime Endpoint
    │
    ├── Runtime Instrument
    │       ├── Runtime Property
    │       ├── Runtime Command
    │       └── Runtime Event
    │
    ├── Runtime Cache
    │
    └── Endpoint Session
            │
            ▼
      Protocol Context
            │
            ▼
         Transport
```

The diagram represents responsibility and dependency direction. It does not
require an identical implementation object graph.

## Transport

### Responsibility

The Transport provides communication between HASE peers.

It moves transport data without interpreting HASE domain semantics.

### Owns

The Transport owns transport-specific concerns, including:

* opening and closing communication channels;
* transport addressing;
* sending transport data;
* receiving transport data;
* transport-specific buffering;
* transport-specific connection state;
* transport-specific error detection;
* transport-specific diagnostics;
* reconnecting the communication channel where appropriate.

Examples include:

* serial-port communication;
* TCP communication;
* UDP communication;
* BLE communication;
* MQTT communication;
* simulated or loopback communication.

### Does not own

The Transport does not own:

* protocol-version negotiation;
* capability negotiation;
* endpoint identity;
* descriptor interpretation;
* request correlation;
* property synchronization;
* Commands;
* Events;
* runtime cache state.

### Lifetime

A Transport instance may exist before a transport connection is established.

A transport connection may be opened, lost, and re-established during the
lifetime of higher-level runtime components.

Transport state does not imply protocol readiness.

## Protocol Context

### Responsibility

The Protocol Context owns execution of the HASE protocol.

It coordinates transport-independent protocol behavior over a Transport.

The Protocol Context is defined by ADR-0013.

### Owns

The Protocol Context owns protocol concerns, including:

* protocol lifecycle state;
* protocol-version negotiation;
* capability negotiation;
* protocol message processing;
* correlation identifiers;
* outstanding Requests;
* Response correlation;
* protocol timers;
* heartbeat management;
* protocol timeout handling;
* descriptor discovery coordination;
* synchronization coordination;
* Notification processing;
* Stream coordination;
* protocol diagnostics;
* protocol statistics;
* protocol configuration;
* framing state;
* recovery and resynchronization coordination.

### Does not own

The Protocol Context does not own:

* the physical communication channel;
* transport-specific addressing;
* endpoint identity;
* application-visible runtime objects;
* authoritative endpoint state;
* the active Property cache;
* application policy.

### Dependencies

The Protocol Context depends on a Transport abstraction.

It provides protocol services to an Endpoint Session and the runtime model.

### Lifetime

A Protocol Context exists while protocol communication is active.

It may survive temporary transport interruption when the protocol lifecycle
supports recovery.

An implementation may recreate internal protocol objects during recovery,
provided that externally visible protocol behavior remains consistent.

## Endpoint Session

### Responsibility

The Endpoint Session represents the runtime's verified relationship with one
specific endpoint identity.

It provides the ownership boundary for endpoint-specific runtime state.

The Endpoint Session is defined by ADR-0012.

### Owns

The Endpoint Session owns or scopes:

* verified endpoint identity;
* the active descriptor association;
* negotiated endpoint-specific capabilities;
* the active Runtime Cache;
* subscription ownership;
* outstanding endpoint operation ownership;
* active Stream ownership;
* endpoint-specific diagnostics;
* endpoint-specific trace context;
* application associations with the endpoint;
* session continuity across temporary communication interruptions.

### Does not own

The Endpoint Session does not own:

* the transport channel;
* protocol framing;
* request correlation mechanics;
* protocol timers;
* the implementation of protocol negotiation;
* application presentation logic.

### Identity boundary

The Endpoint Session must not be identified solely by:

* transport address;
* COM port;
* IP address;
* endpoint type;
* descriptor identity;
* descriptor compatibility.

A replacement endpoint creates a new Endpoint Session even when it uses the
same transport path and exposes the same descriptor.

### Lifetime

An Endpoint Session begins after sufficient endpoint identity verification.

It may continue across:

* temporary transport disconnection;
* protocol reconnect;
* endpoint reboot;
* capability revalidation;
* descriptor revalidation;
* property resynchronization;
* subscription restoration.

It ends when:

* endpoint identity changes;
* the session is explicitly closed;
* identity can no longer be verified within recovery policy;
* protocol compatibility is lost;
* runtime policy terminates the session.

## Runtime Endpoint

### Responsibility

The Runtime Endpoint is the application-facing runtime representation of an
endpoint.

It coordinates endpoint-level runtime behavior and exposes the endpoint's
instruments and connection state to applications.

### Owns

The Runtime Endpoint owns or coordinates:

* the collection of Runtime Instruments;
* endpoint-level runtime status;
* application-facing endpoint availability;
* access to the active Endpoint Session;
* descriptor-to-runtime-model construction;
* endpoint-level lifecycle notifications;
* endpoint-level orchestration;
* routing between application operations and protocol services.

### Does not own

The Runtime Endpoint does not own:

* transport implementation details;
* protocol framing;
* protocol message correlation;
* endpoint identity verification mechanics;
* authoritative device state.

The Runtime Endpoint must not become the implementation owner of every
transport, protocol, session, and model responsibility.

### Dependencies

The Runtime Endpoint depends on:

* an Endpoint Session;
* runtime descriptor information;
* Runtime Instruments;
* runtime status and lifecycle information.

It uses protocol services through defined architectural boundaries rather than
directly interpreting transport data.

### Lifetime

A Runtime Endpoint may exist while its endpoint is disconnected.

It may retain application-visible structure and historical information when no
active Endpoint Session exists.

An implementation must clearly distinguish the Runtime Endpoint object from
the currently active Endpoint Session.

## Runtime Instrument

### Responsibility

A Runtime Instrument is the runtime representation of one instrument exposed
by an endpoint.

It groups related Properties, Commands, and Events into an application-facing
unit.

### Owns

A Runtime Instrument owns or exposes:

* instrument identity within the endpoint model;
* instrument descriptor association;
* Runtime Properties;
* Runtime Commands;
* Runtime Events;
* instrument-level status;
* instrument-specific runtime metadata.

### Does not own

A Runtime Instrument does not own:

* endpoint identity;
* transport state;
* protocol negotiation;
* connection lifecycle;
* endpoint-wide synchronization policy.

### Dependencies

A Runtime Instrument belongs to one Runtime Endpoint.

Its runtime interactions are scoped to the active Endpoint Session of that
Runtime Endpoint.

### Lifetime

Runtime Instruments are normally constructed from descriptor information.

They may be recreated when a new Endpoint Session exposes a changed
descriptor.

Applications must not assume that a Runtime Instrument instance remains valid
after endpoint replacement or descriptor replacement.

## Runtime Property

### Responsibility

A Runtime Property represents synchronized endpoint state.

The device is authoritative.

The Runtime Property exposes the runtime's best known representation of the
device-owned Property value.

### Owns

A Runtime Property owns or exposes:

* Property descriptor association;
* cached Property value;
* cache validity;
* last update information;
* read availability;
* write availability;
* synchronization status;
* change notifications for applications.

### Cache states

A Runtime Property value may be:

* unknown;
* synchronized;
* stale;
* unavailable.

The lifecycle and synchronization behavior for these states is described in
`RuntimeArchitecture.md` and ADR-0011.

### Write behavior

A Runtime Property write is a request to the endpoint.

The requested value must not become authoritative runtime state merely because
the application requested it.

The active cache is updated only after:

* device confirmation;
* an authoritative Property response;
* an authoritative Property-change notification;
* another protocol-defined synchronization result.

An implementation may expose a pending requested value separately from the
authoritative cached value.

### Lifetime

The active cached value belongs to one Endpoint Session.

Cached values from an ended session may be retained for history or diagnostics
but must not become the active state of a replacement session.

## Runtime Command

### Responsibility

A Runtime Command represents an operation that the runtime may invoke on the
endpoint.

Commands perform actions rather than represent synchronized state.

### Owns

A Runtime Command owns or exposes:

* Command descriptor association;
* invocation availability;
* parameter validation;
* invocation state;
* completion state;
* result information;
* protocol failure information;
* timeout or cancellation state where supported.

### Invocation ownership

Every Command invocation belongs to one Endpoint Session.

A response from another session must not complete an earlier invocation, even
when correlation identifiers are reused.

### Persistent settings

Persistent endpoint configuration should normally be represented as
Properties.

Commands should represent executable operations.

A Command that explicitly commits, stores, resets, imports, exports, or applies
configuration may still operate on persistent state when that action is itself
meaningful.

### Lifetime

The Runtime Command definition normally follows the instrument descriptor.

Individual Command invocations have shorter lifetimes and end through:

* successful completion;
* unsuccessful protocol completion;
* timeout;
* cancellation;
* synchronization loss;
* session termination.

## Runtime Event

### Responsibility

A Runtime Event represents a transient endpoint-originated occurrence.

Events are observed by the runtime but are not stored as current endpoint
state.

### Owns

A Runtime Event owns or exposes:

* Event descriptor association;
* subscription availability;
* event payload definition;
* event occurrence notifications;
* event metadata such as occurrence time where available.

### Event distinction

A Runtime Event is distinct from a Property-change notification.

A Property-change notification updates synchronized runtime state.

An Event reports that an occurrence took place.

### Session scope

An Event occurrence belongs to the Endpoint Session in which it was received.

Delayed Events from a previous session must not be presented as occurrences
from a replacement endpoint.

### Lifetime

The Runtime Event definition follows descriptor information.

Individual Event occurrences are transient.

Applications may store Event history outside the active runtime state.

## Runtime Cache

### Responsibility

The Runtime Cache stores the runtime's synchronized representation of
device-owned state.

The device remains authoritative.

### Scope

The active Runtime Cache belongs to one Endpoint Session.

It must not be silently reassigned to another endpoint.

### Contains

The Runtime Cache may contain:

* Property values;
* Property validity state;
* synchronization timestamps;
* descriptor validation information;
* endpoint state required for synchronization;
* subscription-related state;
* selected protocol validation metadata.

Protocol implementation state such as correlation identifiers and framing
buffers does not belong to the Runtime Cache.

### Validity

Cache validity depends on protocol lifecycle state.

Typical behavior is:

* before synchronization, values are unknown;
* during synchronization, values are progressively populated;
* while Operational, values represent the best known authoritative state;
* after synchronization loss, previously known values become stale;
* during resynchronization, values are validated, refreshed, reused, or
  discarded;
* after session replacement, the previous active cache is detached from the
  new session.

### Historical data

An implementation may retain old cache values for:

* diagnostics;
* tracing;
* comparison;
* chart history;
* audit history;
* user-interface continuity.

Historical retention must not imply that retained values remain current.

## Ownership summary

The primary ownership boundaries are:

| Concern                            | Architectural owner |
| ---------------------------------- | ------------------- |
| Communication channel              | Transport           |
| Transport connection state         | Transport           |
| Protocol execution                 | Protocol Context    |
| Protocol lifecycle                 | Protocol Context    |
| Request correlation                | Protocol Context    |
| Protocol timers and heartbeat      | Protocol Context    |
| Verified endpoint identity         | Endpoint Session    |
| Active descriptor association      | Endpoint Session    |
| Active synchronized cache          | Endpoint Session    |
| Endpoint-level application model   | Runtime Endpoint    |
| Instrument-level application model | Runtime Instrument  |
| Synchronized state value           | Runtime Property    |
| Executable endpoint operation      | Runtime Command     |
| Transient endpoint occurrence      | Runtime Event       |

## Interaction flow

A typical Property read follows this conceptual flow:

```text
Application
    │
    ▼
Runtime Property
    │
    ▼
Runtime Instrument
    │
    ▼
Runtime Endpoint
    │
    ▼
Endpoint Session
    │
    ▼
Protocol Context
    │
    ▼
Transport
    │
    ▼
Endpoint device
```

The response returns through the same architectural boundaries.

The Protocol Context correlates the Response.

The Endpoint Session validates that the result belongs to the active endpoint
identity.

The Runtime Property updates the active cache only with an authoritative
result.

## Dependency rules

The following dependency rules apply:

* application-facing runtime models must not depend on transport-specific
  implementations;
* Transport must not depend on runtime descriptors or endpoint models;
* Protocol Context may depend on transport abstractions but not on
  application-specific runtime views;
* Endpoint Session may depend on Protocol Context services but must not own
  transport implementation details;
* Runtime Cache must be scoped to exactly one Endpoint Session;
* Runtime Instruments, Properties, Commands, and Events belong to one Runtime
  Endpoint;
* protocol messages must not directly modify application-visible state without
  session and lifecycle validation;
* endpoint replacement must not reuse active state without explicit
  application-level migration.

## Component creation

The exact factory, dependency-injection, composition, and construction strategy
is not defined by this document.

However, component creation must preserve these architectural boundaries.

A typical composition sequence may include:

1. create or select a Transport;
2. create protocol execution state;
3. establish protocol compatibility;
4. verify endpoint identity;
5. establish an Endpoint Session;
6. obtain or validate descriptors;
7. create or update the Runtime Endpoint model;
8. synchronize the Runtime Cache;
9. expose the endpoint as Operational.

This sequence is conceptual and does not prescribe concrete APIs.

## Simulation

Simulation should use the same runtime component boundaries where practical.

A simulated endpoint may replace a physical Transport and device while
preserving:

* Protocol Context behavior;
* Endpoint Session behavior;
* runtime lifecycle behavior;
* Runtime Endpoint and Runtime Instrument models;
* Property, Command, and Event semantics.

Simulation-specific shortcuts must not redefine the meaning of runtime
components.

## Gateways

A gateway may expose multiple endpoint identities through one transport and
one protocol connection.

In such a design:

* Transport remains shared;
* Protocol Context may be shared or partitioned by protocol profile;
* each downstream endpoint has its own Endpoint Session;
* each Endpoint Session has its own active Runtime Cache;
* endpoint-specific operations remain session-scoped.

The final gateway mapping will be defined by future protocol and gateway
architecture decisions.

## Extensibility

Future runtime components should be introduced only when they define a clear
new responsibility that does not fit an existing owner.

Potential future components include:

* descriptor repositories;
* security contexts;
* authentication contexts;
* tracing services;
* discovery coordinators;
* gateway coordinators;
* persistence services;
* firmware-update coordinators.

Such additions must preserve the existing separation between Transport,
Protocol Context, Endpoint Session, and runtime models.

## Related decisions

* ADR-0008 defines Properties, Commands, and Events.
* ADR-0009 defines protocol capabilities.
* ADR-0010 defines protocol messages.
* ADR-0011 defines protocol lifecycle.
* ADR-0012 defines Endpoint Sessions.
* ADR-0013 defines the Protocol Context.

# RuntimeComponentModel.md

Update the Protocol Context section so that framing state is not described as
an owned Protocol Context responsibility.

Replace any statement equivalent to:

```text
The Protocol Context owns framing state.
```

with:

```text
The Protocol Context owns protocol execution above the serialization and
framing boundary.

Framing state belongs to transport infrastructure below the Protocol Context.
The Protocol Context may observe framing failures but does not process partial
frames or raw transport data.
```

Add the following component section between Protocol Context and Transport, or
place it where it best matches the existing component order:

## Serializer

### Responsibility

The Serializer converts one protocol message into one serialized message and
reconstructs one protocol message from one serialized message.

### Owns

The Serializer owns:

* protocol-message encoding;
* protocol-message decoding;
* serialized field representation;
* serialization-version handling;
* serialization-format validation;
* reporting serialization failures.

### Does not own

The Serializer does not own:

* transport communication;
* frame boundary detection;
* endpoint identity;
* protocol lifecycle;
* request correlation;
* Runtime Cache updates.

### Dependencies

The Serializer operates between the Protocol Context and the Framer.

The concrete serialization formats are defined by ADR-0015.

## Framer

### Responsibility

The Framer maps one serialized protocol message to one HASE frame and
reconstructs complete frames from transport data.

### Owns

The Framer owns:

* frame boundary detection;
* frame-length validation;
* frame-size enforcement;
* framing buffers;
* framing integrity validation where required;
* reconstruction of complete frames;
* framing-error reporting;
* framing synchronization and recovery according to the selected framing
  profile.

### Does not own

The Framer does not own:

* protocol-message semantics;
* endpoint identity;
* request correlation;
* Property synchronization;
* Command execution;
* Event interpretation;
* semantic Stream state.

### Frame invariant

Each frame contains exactly one complete serialized protocol message.

Transport-level segmentation may divide a frame into smaller native transport
units, but the Framer must reconstruct the complete frame before passing the
serialized message to the Serializer.

### Dependencies

The Framer operates between the Serializer and the Transport.

Update the ownership summary with:

| Concern                                  | Architectural owner |
| ---------------------------------------- | ------------------- |
| Protocol-message encoding and decoding   | Serializer          |
| Frame boundaries and frame validation    | Framer              |
| Transport segmentation and communication | Transport           |

Update the dependency rules with:

* Protocol Context must not depend on raw transport data or partial frames.
* Serializer must not depend on transport-specific addressing or connection
  state.
* Framer must not interpret protocol-message semantics.
* Transport fragments must be reassembled before deserialization.
* Each frame must contain exactly one serialized protocol message.

# RuntimeArchitecture.md

Add the following section:

## Framing and serialization boundary

The runtime protocol layer operates on complete protocol messages.

Raw transport data is processed below the Protocol Context through framing and
serialization infrastructure.

On receive:

1. the Transport supplies transport data;
2. the Framer reconstructs one complete HASE frame;
3. the Serializer reconstructs one protocol message;
4. the Protocol Context processes the protocol message;
5. the Endpoint Session and runtime model validate and apply endpoint-specific
   results.

On send, the sequence is reversed.

Partial frames, frame delimiters, transport packets, and transport-level
segments are not exposed to the Protocol Context.

Framing, serialization, transport, and protocol failures remain distinct so
that lifecycle and recovery logic can respond appropriately.

See ADR-0014 and `RuntimeComponentModel.md`.


# Serializer

## Responsibility

The Serializer converts protocol messages into serialized representations and
reconstructs protocol messages from serialized representations.

The Serializer defines how protocol information is represented.

It does not define how serialized data is transported.

## Owns

The Serializer owns:

* protocol message encoding;
* protocol message decoding;
* serialization format interpretation;
* serialization version handling;
* serialized field representation;
* validation of serialized content;
* reporting serialization failures.

## Does not own

The Serializer does not own:

* transport communication;
* transport buffering;
* frame boundary detection;
* endpoint identity;
* protocol lifecycle;
* request correlation;
* synchronization;
* Runtime Cache updates.

## Dependencies

The Serializer operates between the Protocol Context and the Framer.

It exchanges complete protocol messages with the Protocol Context.

It exchanges complete serialized messages with the Framer.

The Serializer never processes partial frames or raw transport fragments.

## Lifetime

The Serializer exists for the lifetime of protocol communication.

An implementation may recreate Serializer instances without affecting protocol
semantics, provided serialization compatibility is preserved.

---

# Framer

## Responsibility

The Framer maps one serialized protocol message to one HASE frame.

On reception it reconstructs complete frames from transport data before
passing serialized messages to the Serializer.

The Framer owns message boundaries.

## Owns

The Framer owns:

* frame boundary detection;
* frame construction;
* frame reconstruction;
* framing buffers;
* frame-size enforcement;
* frame validation;
* framing synchronization;
* framing recovery;
* framing error reporting.

## Does not own

The Framer does not own:

* protocol message semantics;
* endpoint identity;
* request correlation;
* protocol lifecycle;
* Property synchronization;
* Command execution;
* Event interpretation;
* semantic Stream processing.

## Frame invariant

Every HASE frame contains exactly one complete serialized protocol message.

Transport implementations may divide a frame into multiple native transport
units.

The Framer reconstructs the complete frame before passing the serialized
message to the Serializer.

Neither the Serializer nor the Protocol Context observes partial frames.

## Dependencies

The Framer operates between the Serializer and the Transport.

The Framer depends on transport communication.

The Serializer depends on the Framer.

The Protocol Context depends only on the Serializer.

## Lifetime

The Framer exists while framed communication is active.

An implementation may maintain internal receive buffers, synchronization
state, and temporary reconstruction state without exposing those details to
higher architectural layers.

---

# Updated communication pipeline

The communication pipeline is now:

```text
Application
        │
        ▼
Runtime Endpoint
        │
        ▼
Endpoint Session
        │
        ▼
Protocol Context
        │
        ▼
Serializer
        │
        ▼
Framer
        │
        ▼
Transport
```

Each layer owns exactly one primary architectural responsibility.

Higher layers remain independent of lower-layer implementation details.

No layer bypasses the responsibilities of the layers beneath it.



