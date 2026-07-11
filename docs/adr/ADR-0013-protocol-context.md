# ADR-0013: Protocol Context

## Status

Accepted

## Context

ADR-0008 defines the interaction model of Properties, Commands, and Events.

ADR-0009 defines protocol capability negotiation.

ADR-0010 defines the protocol message model.

ADR-0011 defines the protocol connection lifecycle.

ADR-0012 introduces the Endpoint Session as the verified relationship between
the runtime and one endpoint identity.

The runtime architecture now distinguishes:

* transport communication;
* protocol communication;
* endpoint identity;
* synchronized endpoint state.

The protocol layer requires a dedicated architectural component responsible
for executing the HASE protocol.

This responsibility should remain independent of both transport-specific
implementation details and endpoint-specific runtime state.

Without this separation, protocol execution responsibilities would become
distributed across transport implementations, RuntimeEndpoint, Endpoint
Session, and runtime cache components.

Such a design would reduce modularity, complicate testing, and make protocol
evolution more difficult.

## Decision

The HASE runtime defines a Protocol Context as the architectural owner of
protocol execution.

The Protocol Context is a protocol-layer concept.

It is independent of:

* transport implementation;
* endpoint identity;
* endpoint descriptor;
* runtime Property cache;
* application logic.

The Protocol Context executes protocol behaviour throughout the lifetime of a
protocol connection.

The architecture defines the responsibility of the Protocol Context but does
not prescribe its implementation.

A runtime may implement the Protocol Context as one object, multiple
collaborating objects, immutable state, actors, services, or another
appropriate design.

### Responsibility

The Protocol Context owns protocol execution.

Its responsibilities include:

* protocol lifecycle management;
* protocol-version negotiation;
* capability negotiation;
* protocol message correlation;
* request tracking;
* response correlation;
* protocol timers;
* heartbeat management;
* synchronization management;
* stream management;
* protocol statistics;
* protocol diagnostics;
* framing state;
* protocol configuration.

The Protocol Context coordinates protocol behaviour but does not own endpoint
identity or endpoint-specific runtime data.

### Architectural boundaries

The Protocol Context separates protocol execution from transport management,
endpoint identity, and runtime state.

The architectural responsibilities are divided as follows.

#### Transport

The transport layer is responsible for moving data between communication
peers.

Typical responsibilities include:

* establishing communication;
* detecting transport failures;
* sending transport frames;
* receiving transport frames;
* transport-specific buffering;
* transport-specific error detection.

The transport does not interpret HASE protocol semantics.

#### Protocol Context

The Protocol Context executes the HASE protocol.

It interprets protocol messages, manages protocol state, coordinates protocol
operations, and determines how the runtime communicates with an endpoint.

The Protocol Context does not own endpoint identity or application-visible
runtime data.

#### Endpoint Session

The Endpoint Session represents the runtime's verified relationship with one
endpoint identity.

It owns endpoint-specific context rather than protocol execution.

Examples include:

* endpoint identity;
* descriptor association;
* synchronized Property cache;
* subscription ownership;
* endpoint-specific diagnostics.

The Endpoint Session relies on the Protocol Context to establish and maintain
protocol communication.

#### Runtime model

The runtime model exposes endpoint functionality to applications.

Applications interact with synchronized runtime objects rather than directly
with protocol messages or transport frames.

### Ownership

Each architectural layer owns one primary responsibility.

The Transport owns communication.

The Protocol Context owns protocol execution.

The Endpoint Session owns endpoint identity.

The runtime model owns synchronized endpoint representation.

Responsibilities should not be duplicated between these architectural layers.

### Lifetime

A Protocol Context exists while protocol communication is active.

A Protocol Context is created when protocol communication begins.

It is destroyed when protocol communication permanently ends.

A Protocol Context may survive temporary transport interruptions when the
protocol lifecycle permits recovery.

An Endpoint Session may continue across several protocol reconnections while
using the same Protocol Context.

Alternatively, an implementation may recreate the Protocol Context during
recovery provided that externally observable protocol behaviour remains
consistent.

The architecture intentionally permits implementation flexibility.

### Protocol execution

The Protocol Context coordinates protocol execution.

Typical protocol activities include:

* negotiating protocol compatibility;
* negotiating capabilities;
* validating endpoint identity;
* coordinating descriptor discovery;
* synchronizing Properties;
* tracking outstanding Requests;
* correlating Responses;
* processing Notifications;
* coordinating Streams;
* detecting protocol timeouts;
* initiating protocol recovery;
* performing protocol resynchronization.

The Protocol Context does not determine application behaviour.

Instead, it provides protocol services to the runtime.

### Requests and Responses

Outstanding Requests belong to the Protocol Context.

Correlation identifiers are interpreted within the Protocol Context.

A Response shall be matched to the corresponding Request by the Protocol
Context before being forwarded to higher runtime layers.

Protocol timeout handling is also a Protocol Context responsibility.

### Notifications

Notifications are interpreted by the Protocol Context.

The Protocol Context determines whether a Notification:

* updates synchronized Property state;
* represents an Event;
* affects protocol execution;
* belongs to an active Endpoint Session.

Notifications that cannot be associated with the active protocol state shall
be rejected or ignored according to protocol rules.

### Streams

Stream coordination belongs to the Protocol Context.

The Protocol Context tracks stream progress, validates stream continuity, and
coordinates stream recovery where supported by the protocol.

Endpoint-specific interpretation of streamed data belongs outside the Protocol
Context.

### Diagnostics

The Protocol Context collects protocol diagnostics.

Examples include:

* message counters;
* protocol errors;
* protocol timeouts;
* retry statistics;
* synchronization statistics;
* protocol-version information;
* negotiated capabilities.

Transport diagnostics remain the responsibility of the transport layer.

Endpoint diagnostics remain the responsibility of the Endpoint Session or
runtime model.

### Configuration

Protocol configuration belongs to the Protocol Context.

Examples include:

* timeout values;
* heartbeat policy;
* retry policy;
* protocol limits;
* negotiated protocol parameters.

Transport configuration belongs to the transport layer.

Endpoint configuration exposed as Properties belongs to the endpoint model.

### Extensibility

Future protocol extensions should integrate through the Protocol Context.

Examples include:

* secure protocol negotiation;
* authenticated sessions;
* compression;
* protocol tracing;
* protocol extensions;
* additional stream types;
* future protocol versions.

The Protocol Context provides a stable architectural boundary for protocol
evolution.

## Consequences

### Positive

* Protocol execution has a clearly defined architectural owner.
* Runtime responsibilities remain well separated.
* Transport implementations remain protocol-independent.
* Endpoint Sessions remain identity-focused.
* Runtime models remain application-focused.
* Protocol execution can evolve independently of endpoint models.
* Testing protocol behaviour becomes significantly easier.

### Negative

* Runtime implementations introduce another architectural layer.
* Protocol execution requires explicit coordination between architectural
  components.
* Clear interfaces are required between Transport, Protocol Context, Endpoint
  Session, and runtime model.

## Alternatives considered

### RuntimeEndpoint owns protocol execution

Rejected because RuntimeEndpoint already coordinates endpoint-specific runtime
behaviour and should not also become responsible for protocol execution.

### Transport owns protocol execution

Rejected because protocol semantics are intentionally independent of transport
implementation.

### Endpoint Session owns protocol execution

Rejected because endpoint identity and protocol execution are separate
responsibilities.

### No explicit Protocol Context

Rejected because protocol responsibilities would become distributed across
multiple runtime components, reducing modularity and making future protocol
extensions more difficult.

## Relationship to previous ADRs

ADR-0008 defines interaction semantics.

ADR-0009 defines protocol capabilities.

ADR-0010 defines protocol messages.

ADR-0011 defines protocol lifecycle.

ADR-0012 defines Endpoint Sessions.

This ADR defines the architectural owner responsible for executing those
protocol concepts throughout the lifetime of a protocol connection.

