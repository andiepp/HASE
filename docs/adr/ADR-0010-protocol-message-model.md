# ADR-0010: Protocol Message Model

## Status

Accepted

## Context

ADR-0008 defines the interaction model of Properties, Commands, and Events.

ADR-0009 defines capability negotiation.

The protocol now requires a transport-independent model describing how all
protocol exchanges are represented.

The message model must remain independent of framing, serialization,
transport, binary layout, JSON representation, or packet structure.

The message model defines the semantic structure of protocol communication.

## Decision

All communication between a HASE runtime and an endpoint consists of protocol
messages.

A protocol message is an immutable semantic object.

Protocol messages are independent of:

- transport;
- framing;
- serialization;
- encoding;
- packet boundaries.

Each protocol message belongs to exactly one protocol connection.

### Message categories

The protocol defines four semantic message categories.

- Request
- Response
- Notification
- Stream

These categories define protocol behaviour rather than implementation classes.


### Request

A Request asks the peer to perform an operation.

Examples include:

- read a property;
- write a property;
- invoke a command;
- negotiate capabilities;
- request a descriptor.

A Request may expect a Response.

Each Request that expects a Response owns a correlation identifier.

The exact representation of the correlation identifier is not defined by this
ADR.

### Response

A Response completes exactly one Request.

A Response references the correlation identifier of its Request.

A Response represents either:

- successful completion; or
- unsuccessful completion.

Protocol failures are represented by unsuccessful Responses rather than by a
separate message category.

A Response may contain:

- returned values;
- command results;
- status information;
- protocol error information.

Transport failures are not represented by Responses.

### Notification

A Notification represents information originating from one peer without a
preceding Request.

Notifications are asynchronous.

Examples include:

- property-change notifications;
- Events;
- endpoint status changes;
- connection-related protocol notifications.

Notifications do not require correlation identifiers.

### Stream

A Stream represents the transfer of data that is larger than a single protocol
message or that is naturally transferred as a sequence.

Examples include:

- firmware update;
- diagnostic logs;
- waveform captures;
- calibration data;
- future bulk-transfer operations.

The Stream model defines semantic data transfer.

The framing and segmentation of stream data are not defined by this ADR.


### Correlation

Request/Response exchanges use correlation identifiers.

Each terminal Response corresponds to exactly one Request.

The protocol does not permit multiple terminal Responses for a single Request.

Notifications are not correlated.

Stream operations may define their own transfer identifiers in future protocol
extensions.

### Errors

Protocol errors are represented inside Responses.

Examples include:

- unknown Property;
- unknown Command;
- access denied;
- unsupported capability;
- invalid parameter;
- endpoint busy.

Transport failures remain outside the protocol model.

Examples include:

- timeout;
- disconnected transport;
- framing failure;
- checksum failure;
- transport reset.

The runtime must distinguish protocol failures from transport failures.

### Transport independence

The message model is transport-independent.

The same protocol messages may be transported over:

- UART;
- TCP;
- UDP;
- BLE;
- MQTT;
- future transports.

Transport adapters are responsible for mapping protocol messages to transport
frames.

The message model itself contains no transport-specific assumptions.

### Extensibility

New protocol operations are introduced by defining new Request,
Response, Notification, or Stream message types.

The semantic message categories remain stable.

Future protocol versions may extend existing message definitions while
preserving compatibility through protocol-version negotiation.

## Consequences

### Positive

- A single message model supports all transports.
- Request completion is unambiguous.
- Protocol failures and transport failures are clearly separated.
- Notifications remain independent from synchronous operations.
- Future bulk transfers fit naturally into the protocol.
- Runtime APIs can operate entirely on protocol messages.

### Negative

- Correlation identifiers become mandatory for request/response exchanges.
- Stream transfers require additional protocol definition.
- Runtime implementations must separately manage transport failures and
protocol failures.

## Alternatives considered

### Separate Error message category

Rejected because unsuccessful completion is naturally represented as a
Response.

### Transport-specific message definitions

Rejected because transports should not influence protocol semantics.

### One message type per protocol operation

Rejected because the protocol benefits from stable semantic message
categories that remain unchanged as operations evolve.

### Firmware-specific transfer messages

Rejected because future protocol versions are expected to transfer many
different kinds of large data.


