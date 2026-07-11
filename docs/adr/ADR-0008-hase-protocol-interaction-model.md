# ADR-0008: HASE Protocol Interaction Model

## Status

Accepted

## Context

HASE requires a protocol between devices and the runtime.

The protocol must support endpoints ranging from resource-constrained
microcontrollers to feature-rich embedded systems.

Resource-constrained endpoints may provide only compact descriptors and a
small subset of protocol functionality. Feature-rich endpoints may provide
JSON descriptors, subscriptions, commands, events, firmware update, and
other advanced capabilities.

The protocol therefore requires a common interaction model that does not
assume that every endpoint supports every protocol feature.

The HASE runtime also requires a clear ownership model for endpoint state.
Without such a model, runtime state, device state, property writes, command
execution, and event delivery could have ambiguous semantics.

## Decision

The HASE protocol is based on three interaction types:

- Properties
- Commands
- Events

### Properties

Properties represent observable endpoint state.

The device is authoritative for property values.

The runtime maintains a synchronized cache of the property values known to
it. The runtime cache is not an independent authoritative state store.

A property may be:

- readable;
- writable;
- read-only;
- persistent;
- volatile;
- subscribable;
- pollable.

The exact supported operations are determined by the endpoint descriptor and
negotiated protocol capabilities.

A runtime request to change a property is a request to the device. The runtime
must not treat the requested value as authoritative until the device has
accepted the change or reported the resulting value.

Property synchronization may use polling, subscriptions, explicit
notifications, or another capability-negotiated mechanism.

A property-change notification communicates a new authoritative property
value. It is not modeled as an application event.

### Commands

Commands represent explicit operations invoked by the runtime on the device.

A command may:

- accept parameters;
- return a result;
- complete without a result;
- fail;
- be rejected;
- time out;
- be unavailable in the current device state.

Commands are distinct from properties.

A persistent device setting should normally be represented as a property.
An operation that performs an action should normally be represented as a
command.

The command protocol must support correlation between an invocation and its
completion, result, or error when the endpoint capabilities permit command
responses.

### Events

Events represent transient occurrences originating from the device.

Events are observed by the runtime but are not synchronized as current
endpoint state.

An event may contain event-specific data.

Events are distinct from property-change notifications:

- a property-change notification updates synchronized state;
- an event reports that an occurrence took place.

Event delivery may depend on negotiated endpoint capabilities. Endpoints that
do not support asynchronous events may omit event support or expose relevant
state through pollable properties where appropriate.

### State authority

The device is authoritative.

The runtime is a synchronized cache.

The runtime may temporarily contain stale, incomplete, or unknown property
values when:

- a connection has not yet been established;
- initial synchronization has not completed;
- communication has been interrupted;
- the device has changed state since the last synchronization;
- the endpoint does not expose a synchronization mechanism for a value.

Runtime APIs and state models must not imply that cached values are always
current.

### Capability negotiation

The protocol uses capability negotiation so that endpoints and runtimes can
determine which protocol features are mutually supported.

Capabilities may include, but are not limited to:

- compact descriptors;
- JSON descriptors;
- property reads;
- property writes;
- property polling;
- property subscriptions;
- commands;
- command responses;
- events;
- firmware update.

Capability negotiation defines available protocol mechanisms. The descriptor
defines the endpoint's domain model and the operations exposed by that
endpoint.

Unsupported optional capabilities must not prevent basic communication when
a mutually supported protocol subset exists.

The exact wire representation of capability negotiation is not decided by
this ADR.

### Protocol extensibility

Properties, commands, and events form the stable semantic interaction model.

Wire encoding, framing, transport mapping, compact representations, and
additional optional capabilities may evolve without changing this interaction
model.

Protocol extensions must preserve compatibility through explicit versioning,
capability negotiation, or both.

## Consequences

### Positive

- The device/runtime ownership model is explicit.
- Runtime state can be modeled as synchronized, stale, unknown, or
  disconnected without ambiguity.
- Persistent settings and executable operations have distinct semantics.
- Property notifications and transient events are not conflated.
- Small and large embedded devices can implement different protocol subsets.
- JSON and compact descriptors can coexist within one protocol architecture.
- Future features such as subscriptions and firmware update can be introduced
  through capabilities.

### Negative

- Runtime implementations must handle stale and unknown cached state.
- Property writes require confirmation or authoritative synchronization.
- Capability negotiation adds protocol complexity.
- Tests must cover multiple capability combinations.
- Applications cannot assume that all endpoints support commands,
  subscriptions, events, or rich descriptors.

## Alternatives considered

### Runtime-authoritative state

Rejected because the physical device owns the actual hardware and persistent
device state. A runtime-side value cannot override reality when communication
is delayed or unavailable.

### Commands for all device interactions

Rejected because observable state, persistent configuration, operations, and
transient occurrences have different lifecycle and synchronization semantics.

### Properties and commands only

Rejected because transient device-originated occurrences should not be forced
into persistent property state or represented as unsolicited command
responses.

### One mandatory full protocol profile

Rejected because it would exclude resource-constrained microcontrollers and
would make the minimum device implementation unnecessarily expensive.

### Separate protocols for constrained and feature-rich devices

Rejected because this would duplicate concepts, fragment interoperability,
and require the runtime to support unrelated device models. Capability-based
profiles provide a common semantic foundation with different implementation
costs.