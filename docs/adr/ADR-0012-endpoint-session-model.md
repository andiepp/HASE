# ADR-0012: Endpoint Session Model

## Status

Accepted

## Context

ADR-0008 defines the Properties, Commands, and Events interaction model.

ADR-0009 defines protocol capability negotiation.

ADR-0010 defines the transport-independent protocol message model.

ADR-0011 defines the protocol connection lifecycle and distinguishes protocol
state from transport state.

The runtime also requires a stable concept representing its relationship with
one specific endpoint.

A transport connection alone cannot provide this identity.

Examples include:

* a serial port may remain assigned to the same COM port after a device has
  been replaced;
* a TCP connection may be re-established to the same address while the
  endpoint hardware has changed;
* an endpoint may reboot without changing its identity;
* firmware may be updated while the physical endpoint remains the same;
* a gateway may expose different downstream endpoints through the same
  transport;
* two endpoints may expose compatible descriptors but represent different
  physical devices.

The protocol connection lifecycle defines when communication is operational,
but it does not fully define whether the runtime is still communicating with
the same endpoint.

The runtime therefore requires an Endpoint Session concept that is independent
of both transport connection identity and individual protocol connection
instances.

## Decision

HASE defines an Endpoint Session as a first-class architectural concept.

An Endpoint Session represents the runtime's verified relationship with one
specific endpoint identity.

The Endpoint Session is separate from:

* the transport connection;
* the protocol connection;
* the endpoint descriptor;
* the runtime cache.

A session may survive temporary transport disconnections and protocol
reconnections when the endpoint identity remains unchanged.

A new session must be created when the verified endpoint identity changes.

### Architectural layers

The connection architecture consists of three distinct layers.

#### Transport connection

The transport connection provides communication.

Examples include:

* an opened serial port;
* a TCP connection;
* a BLE connection;
* an MQTT communication path.

A transport connection identifies a communication path. It does not reliably
identify the endpoint using that path.

#### Protocol connection

The protocol connection establishes compatible HASE protocol communication
over a transport connection.

It includes:

* protocol-version negotiation;
* capability negotiation;
* descriptor discovery or validation;
* synchronization;
* operational protocol communication.

A protocol connection may be re-established several times during one Endpoint
Session.

#### Endpoint Session

The Endpoint Session binds the runtime to a verified endpoint identity.

It provides continuity across transport interruptions and protocol
resynchronization when the same endpoint returns.

The session is the runtime-level context in which endpoint-specific state,
history, subscriptions, diagnostics, and application associations may be
maintained.

### Session establishment

An Endpoint Session is established only after the runtime has obtained
sufficient information to verify the endpoint identity.

Session establishment may occur during protocol negotiation, capability
negotiation, descriptor discovery, or another defined identity-verification
step.

A session must not be considered established merely because:

* a transport connection exists;
* protocol messages can be exchanged;
* a descriptor is compatible;
* the same transport address is being used;
* the same endpoint type is reported.

The exact messages and encoding used to establish endpoint identity are not
defined by this ADR.

### Endpoint identity

Endpoint identity represents the identity of the endpoint instance with which
the runtime communicates.

Endpoint identity must be stable enough to distinguish:

* a reboot of the same endpoint;
* a replacement endpoint using the same transport path;
* two devices of the same type;
* two devices exposing identical descriptors.

The identity model may include one or more identity components, such as:

* a device-assigned unique identifier;
* a hardware identifier;
* a provisioned identifier;
* a gateway-scoped endpoint identifier;
* an endpoint-instance identifier;
* an identity supplied by a predefined endpoint profile.

The concrete identity representation is not defined by this ADR.

A descriptor is not sufficient endpoint identity because multiple endpoints
may expose the same descriptor.

A transport address is not sufficient endpoint identity because addresses may
be reused or reassigned.

### Session continuity

A temporary loss of transport or protocol synchronization does not
automatically end the Endpoint Session.

The session may continue when:

* the transport reconnects;
* the endpoint reboots;
* protocol negotiation is repeated;
* capabilities are renegotiated;
* the descriptor is revalidated;
* subscriptions are restored;
* properties are resynchronized.

Session continuity is permitted only when the runtime verifies that the
returning endpoint has the same endpoint identity.

The runtime must not infer continuity solely from transport location,
descriptor compatibility, cached information, or timing.

### Session replacement

A new Endpoint Session must be created when the verified endpoint identity
differs from the identity of the current session.

Examples include:

* another device is connected to the same serial port;
* an endpoint at the same network address is replaced;
* a gateway exposes a different downstream endpoint;
* endpoint identity information is reset or reprovisioned;
* identity verification proves that the previous endpoint is no longer
  present.

When a new session is detected:

* the previous session is ended;
* pending Commands from the previous session fail;
* active Streams from the previous session terminate;
* subscriptions from the previous session are invalidated;
* cached Properties from the previous session must not be associated with the
  new endpoint;
* descriptor and capability information must be validated for the new
  session;
* applications must be informed that endpoint replacement occurred.

Descriptor compatibility does not permit state from the previous session to be
silently transferred to the new session.

### Relationship to the protocol lifecycle

The protocol lifecycle from ADR-0011 operates within the context of an
Endpoint Session once identity has been verified.

Before identity verification, the runtime may have a transport connection and
a partially negotiated protocol connection but no established Endpoint
Session.

A typical initial sequence is:

Disconnected

↓

Transport Connected

↓

Protocol Negotiation

↓

Endpoint Identity Verification

↓

Endpoint Session Established

↓

Capability Negotiation

↓

Descriptor Discovery

↓

Initial Synchronization

↓

Operational

The exact placement of identity verification within negotiation may vary by
protocol profile, provided that the session is established before endpoint-
specific cached state is trusted or exposed as belonging to that endpoint.

### Resynchronization within a session

Synchronization loss does not necessarily end the session.

When protocol synchronization is lost, the runtime enters the recovery
lifecycle defined by ADR-0011.

During resynchronization, the runtime must verify endpoint identity before
reusing session-specific state.

If the identity matches, the runtime may:

* continue the existing Endpoint Session;
* validate cached capabilities;
* validate the cached descriptor;
* refresh or reuse cached Properties according to protocol rules;
* restore subscriptions;
* return to Operational.

If the identity does not match, the runtime must end the existing session and
establish a new one.

### Session-scoped state

State that logically belongs to one endpoint instance is session-scoped.

Examples include:

* verified endpoint identity;
* negotiated capabilities;
* descriptor association;
* runtime Property cache;
* subscription state;
* outstanding Command ownership;
* active Stream ownership;
* diagnostics and communication statistics;
* endpoint-specific application state;
* trace and logging context.

Not all session-scoped state must survive disconnection.

Each type of state must define whether it is:

* preserved;
* marked stale;
* invalidated;
* recreated;
* transferred only after validation.

### Runtime cache ownership

The runtime cache belongs to an Endpoint Session.

Cached Property values must never be reassigned to another session merely
because the new endpoint has a compatible descriptor.

When a session ends, its cache may be retained for history or diagnostics, but
it is no longer the active cache for a newly established session.

When the same session resumes after successful identity verification, cached
values may be validated or refreshed according to the resynchronization
process.

### Commands and Streams

Commands and Streams belong to the Endpoint Session in which they were
initiated.

A Command response must not complete a Command from another session, even when
correlation identifiers are reused after reconnect.

A Stream must not continue into a different Endpoint Session unless a future
protocol extension explicitly defines a secure transfer-resume mechanism that
verifies both endpoint identity and transfer identity.

Session replacement terminates all outstanding session-bound operations.

### Events and notifications

Events and property-change notifications are interpreted in the context of the
active Endpoint Session.

Notifications received before identity verification must not be exposed as
trusted endpoint activity.

Notifications associated with an ended session must not modify the active
cache of a replacement session.

The protocol implementation must prevent delayed messages from a previous
session from being applied to a new session.

### Application continuity

Applications may associate user-interface state, charts, logging, tracing, or
control workflows with an Endpoint Session.

Temporary transport interruptions may therefore be handled without losing the
application's association with the endpoint.

When endpoint replacement occurs, applications must be able to distinguish it
from temporary disconnection or resynchronization.

Applications may choose to reuse presentation state for a replacement endpoint
of the same type, but this is an application decision and must not be implied
by the protocol runtime.

### Session termination

An Endpoint Session ends when:

* the runtime intentionally closes the session;
* endpoint identity changes;
* identity can no longer be verified within the applicable recovery policy;
* protocol compatibility is lost;
* the endpoint is removed permanently;
* runtime or application policy explicitly terminates the session.

Transport disconnection alone does not necessarily terminate the session.

The timeout or policy determining when a disconnected session is considered
ended is not defined by this ADR.

### Session identifiers

The runtime may assign an internal Session Identifier to distinguish session
instances.

The Session Identifier is not the same as endpoint identity.

Endpoint identity answers:

> Which endpoint is this?

The Session Identifier answers:

> Which runtime session instance represents the relationship with this
> endpoint?

A new runtime session may be established with the same endpoint identity after
a previous session was explicitly terminated.

The exact Session Identifier representation is not defined by this ADR.

### Security considerations

Endpoint identity verification must not be assumed to provide authentication
or cryptographic security.

A stable identifier may distinguish endpoint instances without proving that an
endpoint is trusted.

Authentication, authorization, confidentiality, integrity protection, and
secure identity verification require separate architectural decisions.

Future security mechanisms may strengthen endpoint identity verification
without changing the Endpoint Session concept.

### Extensibility

The Endpoint Session model applies to direct devices, gateways, simulated
endpoints, and future endpoint types.

A gateway may maintain:

* one transport connection;
* one protocol connection;
* multiple endpoint sessions.

Alternatively, a protocol profile may use independent protocol connections for
individual downstream endpoints.

The selected mapping must preserve the rule that session-scoped state belongs
to exactly one verified endpoint identity.

## Consequences

### Positive

* Endpoint identity is separated from transport addressing.
* Device replacement can be distinguished from temporary disconnection.
* Runtime state cannot be silently assigned to the wrong physical endpoint.
* Transport reconnects may preserve application continuity.
* Reboot and firmware-update recovery fit naturally into the lifecycle.
* Commands, Streams, Events, subscriptions, and cached Properties have a clear
  ownership boundary.
* HASE Studio can associate charts, views, logging, and tracing with a stable
  runtime session.
* Gateways can expose multiple endpoint identities without conflating them
  with one communication path.

### Negative

* Endpoint identity must be established before session-specific state is
  trusted.
* Runtime implementations must manage transport, protocol, and session state
  separately.
* Recovery logic must verify identity before reusing cached state.
* Session replacement requires explicit invalidation of pending operations and
  cached information.
* Constrained endpoints require an identity mechanism suitable for their
  resource limits.
* Testing must cover reconnection to both the same endpoint and a replacement
  endpoint.

## Alternatives considered

### Use the transport connection as the session

Rejected because transport paths and addresses can be reused by different
endpoints.

### Use the descriptor as endpoint identity

Rejected because multiple physical endpoints may expose identical
descriptors.

### Create a new session after every reconnect

Rejected because temporary communication failures should not unnecessarily
destroy endpoint continuity.

### Preserve the session without verifying identity

Rejected because a replacement endpoint could inherit cached state,
subscriptions, commands, or application associations belonging to another
device.

### Attach runtime state directly to endpoint type

Rejected because endpoint type identifies a class of devices rather than one
specific endpoint instance.

### Model only protocol connections

Rejected because one endpoint relationship may span several protocol
connection instances during reconnect and resynchronization.

## Relationship to previous ADRs

ADR-0008 defines the endpoint interaction semantics.

ADR-0009 defines connection-scoped protocol capabilities.

ADR-0010 defines protocol messages.

ADR-0011 defines the protocol connection lifecycle.

This ADR defines the verified endpoint relationship within which negotiated
capabilities, descriptors, cached Properties, Commands, Events, Streams, and
application associations are owned.
