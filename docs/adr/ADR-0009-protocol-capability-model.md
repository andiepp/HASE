# ADR-0009: Protocol Capability Model

## Status

Accepted

## Context

ADR-0008 establishes that the HASE protocol supports endpoints ranging from
resource-constrained microcontrollers to feature-rich embedded systems through
capability negotiation.

The protocol now requires a definition of:

- what a capability represents;
- how capabilities are advertised;
- how mutually supported capabilities are determined;
- how capability dependencies are handled;
- how protocol capabilities relate to endpoint descriptors;
- what minimum functionality is required to establish a usable connection.

The capability model must remain independent of the final protocol framing and
encoding.

It must support endpoints that implement only a small protocol subset without
forcing them to implement JSON, subscriptions, asynchronous events, command
responses, or firmware update.

## Decision

### Capability meaning

A protocol capability represents support for a protocol mechanism.

Capabilities describe what an endpoint or runtime implementation is able to
do at the protocol level.

Capabilities do not describe the domain-specific contents of an endpoint.

Examples include:

- support for a descriptor format;
- support for property-read operations;
- support for property subscriptions;
- support for command invocation;
- support for event delivery;
- support for firmware update.

The endpoint descriptor defines the actual Properties, Commands, and Events
exposed by the endpoint.

A negotiated capability therefore enables a mechanism, while the descriptor
defines whether and where that mechanism may be used.

For example:

- negotiated property-write support does not make every property writable;
- negotiated command support does not define which commands exist;
- negotiated event support does not define which events may be emitted.

### Capability ownership

Both the runtime and the endpoint advertise the protocol capabilities they
support.

The effective capability set is the intersection of the capabilities
advertised by both sides.

A capability advertised by only one side is not active for the connection.

Neither side may use a capability that is not part of the effective capability
set.

### Protocol baseline

A HASE protocol implementation must support a minimal negotiation baseline.

The baseline must allow the peers to determine:

- protocol compatibility;
- endpoint identity;
- supported capabilities;
- supported descriptor representation or endpoint profile;
- whether a mutually usable protocol subset exists;
- when an attempted operation is unsupported.

The baseline does not require support for all Properties, Commands, or Events
mechanisms.

The exact baseline messages and their wire representation are not defined by
this ADR.

### Capability categories

Capabilities are grouped conceptually by protocol area.

Initial capability areas include:

#### Discovery and description

- compact descriptor support;
- JSON descriptor support;
- endpoint or profile identification.

#### Properties

- property read;
- property write;
- property polling;
- property subscription;
- property-change delivery.

#### Commands

- command invocation;
- command responses;
- command cancellation.

#### Events

- event delivery;
- event subscription.

#### Lifecycle and maintenance

- diagnostics;
- time synchronization;
- firmware update.

This list defines architectural capability areas. It does not yet define the
final capability identifiers or require every listed capability to become an
independent wire-level flag.

### Capability independence

Capabilities should represent independently optional mechanisms whenever the
mechanisms can reasonably be implemented separately.

The protocol must not define only broad device classes such as constrained or
full-featured.

A constrained endpoint may support a selected subset of advanced mechanisms,
and a feature-rich endpoint may intentionally omit mechanisms it does not
require.

Descriptor formats are capabilities rather than endpoint classes.

An endpoint may support:

- only a compact descriptor;
- only a JSON descriptor;
- both descriptor formats;
- a predefined profile that does not require transferring a complete
  descriptor.

### Capability dependencies

A capability may require one or more other capabilities.

Dependencies must be defined explicitly.

A peer must not advertise an invalid capability combination.

The negotiation process must reject or disable capability combinations whose
dependencies are not satisfied.

Examples include:

- command responses require command invocation;
- command cancellation requires command invocation and command correlation;
- property subscriptions require a property-change delivery mechanism;
- event subscriptions require event delivery;
- firmware update requires the protocol mechanisms defined by the future
  firmware-update design.

The exact dependency table is not defined by this ADR and will be developed
together with the concrete capability definitions.

### Capability negotiation

Capability negotiation determines the effective protocol mechanisms for one
connection.

Negotiation must:

1. establish protocol-version compatibility;
2. exchange supported capabilities;
3. validate capability dependencies;
4. select a mutually supported descriptor format or endpoint profile;
5. determine whether the negotiated subset is usable;
6. make the effective capability set available to the runtime connection.

Negotiation failure must produce an explicit protocol or connection error.

Peers must not silently assume unsupported functionality.

### Relationship to descriptors

Capabilities and descriptors have separate responsibilities.

Capabilities define available protocol mechanisms.

Descriptors define the endpoint model, including:

- Properties;
- Commands;
- Events;
- access characteristics;
- data types;
- constraints;
- identifiers;
- endpoint-specific metadata.

A descriptor may only declare operations that can be supported by the
negotiated protocol mechanisms.

The runtime must evaluate both the negotiated capability set and the
descriptor metadata before exposing an operation as available.

### Extensibility

The capability model must support future capabilities without changing the
Properties, Commands, and Events interaction model.

Unknown optional capabilities must be ignored or reported without causing
failure when the baseline protocol permits continued operation.

Capabilities that alter fundamental protocol interpretation must be governed
by protocol-version compatibility rather than treated as ordinary optional
capabilities.

The concrete capability identifier representation must provide a defined
extension mechanism.

The identifier encoding is not decided by this ADR.

### Connection scope

The effective capability set belongs to a protocol connection.

It may change after reconnection, firmware replacement, endpoint replacement,
protocol-version change, or configuration change.

The runtime must not assume that capabilities from a previous connection are
still valid.

## Consequences

### Positive

- Constrained and feature-rich endpoints use the same protocol architecture.
- Protocol mechanisms are separated from endpoint domain definitions.
- Optional functionality can be added incrementally.
- The runtime can expose only operations that are actually available.
- Descriptor formats can evolve independently.
- Capability combinations can be validated explicitly.
- Reconnection can safely renegotiate changed endpoint functionality.

### Negative

- Connection establishment requires a negotiation phase.
- Capability dependencies add validation complexity.
- Runtime APIs must account for unavailable mechanisms.
- Tests must cover valid and invalid capability combinations.
- Capability identifiers and compatibility rules require careful long-term
  governance.

## Alternatives considered

### Fixed mandatory feature set

Rejected because it would impose excessive requirements on constrained
microcontrollers.

### Separate constrained and full protocol variants

Rejected because it would fragment the protocol and duplicate interaction
semantics.

### Descriptor-only feature discovery

Rejected because a descriptor describes the endpoint model but does not fully
describe the protocol mechanisms supported by both communication peers.

### Runtime assumptions based on endpoint type

Rejected because endpoint classes are too broad and would prevent independent
feature combinations.

### Capabilities advertised only by the endpoint

Rejected because the endpoint must also know which mechanisms the runtime can
support.

### Permanent capabilities associated with an endpoint identity

Rejected because capabilities can change after firmware updates,
reconfiguration, or reconnection through another protocol implementation.