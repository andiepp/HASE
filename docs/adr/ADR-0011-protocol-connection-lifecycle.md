# ADR-0011: Protocol Connection Lifecycle

## Status

Accepted

## Context

ADR-0008 defines the interaction model of Properties, Commands, and Events.

ADR-0009 defines protocol capability negotiation.

ADR-0010 defines the protocol message model.

The protocol now requires a transport-independent definition of how a
connection progresses from initial establishment through normal operation,
failure, recovery, and shutdown.

The lifecycle must remain independent of the underlying transport.

A serial port, TCP connection, BLE connection, or other transport connection
does not by itself imply that the protocol is operational.

Likewise, loss of protocol synchronization does not necessarily imply loss of
the transport connection.

The runtime therefore requires an explicit protocol lifecycle that is
independent of transport state.

## Decision

The HASE protocol defines a connection lifecycle that is independent of the
transport lifecycle.

Transport implementations are responsible only for establishing and
maintaining communication channels.

The protocol layer is responsible for negotiation, synchronization,
operational state, recovery, and orderly shutdown.

Only the Operational state guarantees that the runtime possesses a valid,
synchronized representation of the endpoint.

Protocol recovery is performed through explicit resynchronization rather than
by assuming that every reconnect requires a complete protocol restart.

### Transport state

The transport layer owns transport-specific connection states.

Examples include:

- serial port opened;
- TCP connected;
- BLE connected.

These states indicate only that communication is possible.

They do not imply that protocol communication is operational.

The runtime shall therefore maintain protocol state independently of
transport state.

### Protocol lifecycle

The protocol lifecycle consists of the following states.

- Disconnected
- Transport Connected
- Protocol Negotiation
- Capability Negotiation
- Descriptor Discovery
- Initial Synchronization
- Operational
- Synchronization Lost
- Resynchronizing

### Disconnected

No transport connection exists.

The runtime has no active protocol session.

Cached runtime data shall be considered unavailable.

No protocol messages may be exchanged.

### Transport Connected

A transport connection has been established.

The runtime may exchange protocol messages.

No assumptions shall yet be made regarding protocol compatibility,
capabilities, descriptors, or synchronized endpoint state.

### Protocol Negotiation

Both peers determine protocol compatibility.

Protocol versions and protocol requirements are evaluated.

If protocol compatibility cannot be established, the connection shall be
terminated or reported as incompatible.

### Capability Negotiation

Both peers exchange supported protocol capabilities.

The effective capability set is determined according to ADR-0009.

Operations that are not supported by both peers shall remain unavailable
during the connection.

### Descriptor Discovery

The runtime determines the endpoint description.

Depending on negotiated capabilities this may involve:

- retrieving a descriptor;
- selecting a predefined endpoint profile;
- validating a cached descriptor.

The runtime shall not assume that a descriptor must always be transferred.

Descriptor validity is determined by protocol negotiation.

### Initial Synchronization

The runtime establishes its initial synchronized representation of endpoint
state.

This may include:

- reading property values;
- restoring persistent runtime state;
- establishing subscriptions;
- validating cached information.

The synchronization procedure is determined by negotiated protocol
capabilities.

Completion of initial synchronization transitions the connection into the
Operational state.

### Operational

Operational is the normal protocol operating state.

Only in the Operational state are all negotiated protocol mechanisms fully
available.

The runtime may:

- synchronize Properties;
- invoke Commands;
- receive Events;
- maintain subscriptions;
- initiate stream transfers;
- expose endpoint functionality to applications.

The runtime cache represents the best known authoritative state of the
endpoint.

The runtime shall continue to monitor protocol health while Operational.

The runtime may periodically verify endpoint responsiveness according to the
requirements of the selected transport and protocol profile.

### Synchronization Lost

Synchronization Lost indicates that the runtime can no longer guarantee that
its cached representation of the endpoint remains authoritative.

This state does not necessarily indicate transport failure.

Examples include:

- endpoint reboot;
- protocol timeout;
- heartbeat failure;
- descriptor invalidation;
- capability mismatch;
- protocol reset.

When synchronization is lost:

- cached Properties become stale;
- pending Commands are considered failed;
- active stream transfers are terminated;
- subscriptions are considered invalid until restored.

The runtime shall prevent applications from assuming that cached endpoint
state remains current.

### Resynchronizing

Resynchronizing restores protocol consistency after synchronization has been
lost.

Resynchronization is not equivalent to establishing a completely new
connection.

Instead, the runtime determines which protocol information remains valid and
which information must be refreshed.

Depending on negotiated capabilities, resynchronization may include:

- protocol verification;
- capability verification;
- descriptor validation;
- descriptor reload;
- property synchronization;
- subscription restoration;
- stream recovery where supported.

If validation determines that previously cached information is still valid,
the runtime may reuse that information.

The protocol therefore permits efficient recovery without unnecessarily
repeating the complete connection sequence.

Successful completion returns the connection to the Operational state.

Failure returns the lifecycle to Disconnected or another appropriate recovery
state.

### Lifecycle transitions

The protocol lifecycle is intentionally deterministic.

A typical lifecycle is:

Disconnected

↓

Transport Connected

↓

Protocol Negotiation

↓

Capability Negotiation

↓

Descriptor Discovery

↓

Initial Synchronization

↓

Operational

↓

Synchronization Lost

↓

Resynchronizing

↓

Operational

Transport loss returns the lifecycle to Disconnected.

Protocol incompatibility terminates the lifecycle before Operational.

Implementations may introduce internal implementation states provided they do
not alter the externally observable lifecycle defined by this ADR.

### Runtime responsibilities

The runtime is responsible for maintaining the protocol lifecycle.

This includes:

- tracking protocol state independently of transport state;
- maintaining the synchronized property cache;
- detecting synchronization loss;
- initiating resynchronization;
- exposing lifecycle changes to applications;
- preventing access to unavailable protocol mechanisms.

Applications should rely on lifecycle state rather than transport state when
determining endpoint availability.

### Cache validity

The runtime cache changes validity throughout the lifecycle.

During Initial Synchronization the cache is progressively populated.

During Operational the cache represents the best known authoritative endpoint
state.

During Synchronization Lost cached values become stale.

During Resynchronizing individual cached information may be:

- reused;
- validated;
- refreshed;
- discarded.

The runtime shall clearly distinguish between:

- synchronized values;
- stale values;
- unknown values;
- unavailable values.

Applications should be able to determine the validity of cached information.

### Recovery philosophy

The protocol is designed to recover from temporary failures without assuming
that every interruption requires complete reinitialization.

Recovery should preserve previously validated information whenever protocol
verification confirms that the information remains valid.

This minimizes communication overhead while maintaining consistency between
runtime and endpoint.

Recovery mechanisms shall remain transparent to applications whenever
possible.

## Consequences

### Positive

- Transport state and protocol state are clearly separated.
- Endpoint recovery is deterministic.
- Runtime cache validity is explicitly defined.
- Descriptor reuse becomes possible.
- Capability reuse becomes possible.
- Recovery after firmware update is naturally supported.
- Applications can accurately determine endpoint readiness.
- Reconnection traffic can be minimized.

### Negative

- Runtime implementations require explicit lifecycle management.
- Recovery logic is more sophisticated than simple reconnect behaviour.
- Cache validity must be tracked throughout the lifecycle.
- Additional testing is required for lifecycle transitions.

## Alternatives considered

### Transport connection implies protocol readiness

Rejected because communication availability does not imply protocol
compatibility or synchronized endpoint state.

### Complete reconnect after every interruption

Rejected because descriptors, capabilities and synchronized state may remain
valid after temporary interruptions.

### No explicit Resynchronizing state

Rejected because recovery is a distinct protocol activity that should be
observable by runtime components and applications.

### Always reload the complete descriptor

Rejected because negotiated protocol mechanisms may determine that cached
descriptor information remains valid.

### Merge transport and protocol state

Rejected because transport implementations should remain independent of
protocol semantics.

## Relationship to previous ADRs

ADR-0008 defines the semantic interaction model.

ADR-0009 defines capability negotiation.

ADR-0010 defines protocol message categories.

This ADR defines when those protocol mechanisms become available and how they
remain synchronized throughout the lifetime of a protocol connection.
