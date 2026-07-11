# ADR-0014: Protocol Framing and Transport Mapping

## Status

Accepted

## Context

ADR-0008 defines the Properties, Commands, and Events interaction model.

ADR-0009 defines protocol capabilities.

ADR-0010 defines transport-independent protocol messages.

ADR-0011 defines the protocol connection lifecycle.

ADR-0012 defines Endpoint Sessions.

ADR-0013 defines the Protocol Context as the architectural owner of protocol
execution.

The protocol now requires a definition of how serialized protocol messages are
carried over different transports.

HASE is intended to support transports with different communication
characteristics, including:

* byte-stream transports such as serial communication and TCP;
* packet-oriented transports such as UDP;
* message-oriented transports such as MQTT;
* constrained packet transports such as BLE;
* simulated and loopback transports;
* future transport types.

These transports differ in:

* whether message boundaries are preserved;
* maximum payload size;
* ordering guarantees;
* delivery guarantees;
* duplication behavior;
* corruption detection;
* connection semantics;
* fragmentation behavior.

The HASE protocol message model must remain independent of those differences.

Framing and serialization are separate concerns.

Serialization defines how one protocol message is represented as data.

Framing defines how the boundaries of one serialized message are identified
when that data is carried over a transport.

The architecture therefore requires a framing boundary between serialization
and transport communication.

## Decision

HASE defines framing and serialization as separate architectural layers.

A frame contains exactly one complete serialized protocol message.

A frame must not contain multiple unrelated protocol messages.

A serialized protocol message must not be split across multiple HASE frames.

Large or sequential application data is transferred through the Stream message
model defined by ADR-0010 rather than by creating protocol messages that exceed
the supported frame size.

Framing belongs below the Protocol Context.

The Protocol Context operates on protocol messages and remains independent of
frame representation and transport-specific communication behavior.

### Layering

The relevant communication layers are:

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

On receive, the process is reversed:

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
Deserializer
        │
        ▼
Protocol Message
        │
        ▼
Protocol Context
```

The conceptual layers do not require one implementation class per layer.

Implementations may combine compatible responsibilities internally, provided
that the architectural boundaries remain observable and testable.

### Frame invariant

Each HASE frame contains exactly one serialized protocol message.

The relationship is:

```text
one frame
    contains
one serialized protocol message
    represents
one protocol message
```

This invariant applies independently of transport type.

A transport may divide the frame into smaller transport units or deliver the
frame through multiple read operations.

Such transport-level segmentation does not create additional HASE frames.

The framing layer must reconstruct the complete HASE frame before
deserialization begins.

### Framing responsibility

The framing layer is responsible for:

* identifying the beginning of a frame;
* identifying the end or declared length of a frame;
* enforcing frame-size limits;
* collecting transport data until one complete frame is available;
* rejecting malformed frame structure;
* detecting incomplete frames;
* providing exactly one serialized message payload to the deserializer;
* preserving frame order when the underlying transport provides ordered
  delivery;
* reporting framing failures separately from protocol failures.

The framing layer does not interpret:

* Properties;
* Commands;
* Events;
* protocol Requests;
* protocol Responses;
* protocol Notifications;
* protocol Streams;
* endpoint descriptors;
* endpoint identity.

### Serialization responsibility

Serialization converts one protocol message into one serialized message and
reconstructs one protocol message from one serialized message.

Serialization is independent of:

* transport addressing;
* transport connection state;
* stream buffering;
* frame boundary detection;
* transport reconnect behavior.

The concrete serialization formats are defined separately by ADR-0015.

### Protocol Context boundary

The Protocol Context sends and receives protocol messages.

It does not process:

* partial frames;
* raw transport bytes;
* transport packets;
* frame delimiters;
* frame length prefixes;
* checksums used only by framing;
* transport-level fragmentation.

Framing and serialization failures are reported to the Protocol Context
through defined failure boundaries, but the Protocol Context does not
implement transport-specific recovery.

The Protocol Context may react to repeated framing or serialization failures
by changing protocol lifecycle state according to ADR-0011.

### Transport responsibility

The Transport owns transport communication.

Depending on the transport, this includes:

* opening and closing communication paths;
* reading and writing transport data;
* transport addressing;
* transport-specific buffering;
* transport-specific segmentation;
* transport-specific connection state;
* transport-specific error reporting;
* transport reconnect behavior where applicable.

The Transport does not interpret HASE protocol messages.

A transport adapter integrates:

* the Transport;
* the selected Framer;
* the selected Serializer.

The exact software composition is an implementation decision.

### Stream-oriented transports

Stream-oriented transports do not preserve application message boundaries.

Examples include:

* serial communication;
* TCP;
* named pipes;
* similar continuous byte-stream transports.

For these transports, the framing layer must explicitly determine where each
frame begins and ends.

A stream-oriented framing format may use mechanisms such as:

* a length prefix;
* a delimiter;
* byte stuffing;
* an escape mechanism;
* a combination of length and integrity information.

The concrete framing representation is not defined by this ADR.

The framer must support:

* receiving a frame across multiple transport reads;
* receiving several frames in one transport read;
* preserving surplus data for the next frame;
* recovering from malformed input according to the selected framing format;
* enforcing a maximum frame size before allocating unbounded memory.

A single transport read is not equivalent to a single HASE frame.

### Packet-oriented transports

Packet-oriented transports preserve packet boundaries.

Examples may include:

* UDP datagrams;
* transport-specific packet APIs;
* selected embedded communication buses.

Where the transport packet size is sufficient, one transport packet should
normally carry one complete HASE frame.

A packet-oriented transport must not assume that every packet is valid merely
because the transport preserved its boundary.

Frame validation and frame-size enforcement still apply.

If the transport cannot carry one complete HASE frame in one native packet,
segmentation and reassembly must occur below the HASE frame boundary.

The protocol message must not observe transport fragments.

### Message-oriented transports

Message-oriented transports preserve application-level message boundaries.

Examples include:

* MQTT message payloads;
* brokered messaging systems;
* queue-based transports.

One transport message should normally carry one complete HASE frame.

Broker topics, routing keys, queue names, or similar transport metadata are
transport concerns.

They must not replace protocol fields that are required for protocol
interpretation, identity, lifecycle, or correlation.

Transport metadata may assist routing, but the protocol message must remain
valid within the selected transport profile.

### Constrained transports

Some transports have small maximum native payload sizes.

Examples may include BLE characteristics or constrained radio protocols.

Such transports may segment one HASE frame into multiple transport units.

Segmentation and reassembly belong below the HASE framing boundary.

The receiving framing infrastructure must provide the deserializer with one
complete serialized message.

Transport segments must not be exposed as protocol Stream messages.

Protocol Streams represent semantic large-data transfer and are distinct from
transport segmentation.

### Protocol Streams

The Stream message category from ADR-0010 is used for semantic transfer of
large or sequential data.

Examples include:

* firmware images;
* diagnostic logs;
* waveform captures;
* calibration data;
* large descriptor content;
* future bulk-transfer operations.

A Stream is represented by a sequence of ordinary protocol messages.

Each Stream message is serialized independently.

Each serialized Stream message is carried in exactly one frame.

Therefore:

```text
large semantic transfer
    consists of
multiple Stream protocol messages

each Stream protocol message
    becomes
one serialized message

each serialized message
    becomes
one frame
```

The Stream model does not remove the need for transport-level segmentation
when a frame is larger than the native transport unit.

The two mechanisms solve different problems.

### Frame-size limits

Every framing profile must define a maximum supported frame size.

The maximum frame size protects implementations from:

* unbounded memory allocation;
* malformed length declarations;
* accidental oversized messages;
* resource exhaustion;
* transport misuse.

The frame-size limit applies to the complete framed representation or
serialized payload as defined by the framing profile.

Peers may negotiate or select a mutually supported maximum frame size through
future capability or protocol-profile definitions.

A sender must not transmit a frame larger than the effective supported limit.

A receiver must reject an oversized frame before allocating unbounded storage.

The concrete numeric limits are not defined by this ADR.

### Fragmentation and reassembly

HASE distinguishes between:

* protocol-level Stream messages;
* transport-level segmentation;
* HASE frames.

A HASE frame contains one complete serialized protocol message.

Transport infrastructure may divide that frame into smaller transport units.

The transport and framing infrastructure are responsible for reassembling
those units before deserialization.

The Protocol Context must not receive partially reassembled messages.

HASE does not define protocol-message fragmentation across multiple frames.

A protocol operation requiring more data than one message can carry must use a
Stream or another explicitly defined multi-message protocol operation.

### Ordering

The framing layer preserves the order in which complete frames are delivered
by an ordered transport.

The framing layer does not create ordering guarantees that the underlying
transport cannot provide.

Protocol operations that require ordering must either:

* use a transport profile that guarantees the required ordering;
* provide protocol-level sequence information;
* define another explicit ordering mechanism.

The required ordering behavior for individual protocol operations will be
defined by their protocol specifications.

Framing itself does not reorder frames.

### Delivery, duplication, and loss

Framing does not guarantee successful delivery.

Delivery guarantees belong to:

* the transport;
* the protocol operation;
* or a future reliability mechanism.

A transport may:

* lose data;
* duplicate data;
* reconnect;
* deliver delayed data;
* deliver data out of order.

The framing layer reconstructs and validates frames but does not by itself
provide end-to-end operation reliability.

Request correlation, timeouts, retries, idempotency, duplicate handling, and
operation recovery belong to the Protocol Context and individual protocol
operation definitions.

### Integrity and corruption detection

Transport-level or framing-level integrity checks may be used to detect
corrupted frames.

Examples include:

* checksums;
* cyclic redundancy checks;
* authenticated integrity mechanisms;
* integrity guarantees already provided by the transport.

The required integrity mechanism may differ by transport profile.

A reliable transport checksum does not necessarily provide security or
protection against intentional modification.

The framing layer must distinguish:

* malformed frame structure;
* failed frame-integrity validation;
* incomplete frame data;
* deserialization failure;
* valid protocol-level error Responses.

These failures belong to different architectural layers.

The concrete checksum or integrity algorithm is not defined by this ADR.

### Error boundaries

Errors are classified according to the layer that detects them.

#### Transport failure

Examples include:

* serial-port closure;
* TCP disconnection;
* BLE link loss;
* MQTT client disconnection;
* transport write failure.

Transport failures are reported by the Transport.

#### Framing failure

Examples include:

* invalid frame delimiter sequence;
* impossible frame length;
* oversized frame;
* failed framing checksum;
* incomplete frame when the connection ends;
* unrecoverable framing synchronization loss.

Framing failures are reported by the framing layer.

#### Serialization failure

Examples include:

* malformed serialized data;
* unknown mandatory serialized structure;
* invalid encoded value;
* unsupported serialization version.

Serialization failures are reported by the serializer or deserializer.

#### Protocol failure

Examples include:

* unknown Property;
* unsupported Command;
* invalid operation parameter;
* access denied;
* endpoint busy.

Protocol failures are represented through the protocol message model,
normally as unsuccessful Responses according to ADR-0010.

### Recovery from framing failures

The selected framing profile must define whether and how framing
synchronization can be recovered after malformed input.

For example, a delimiter-based frame format may scan for the next valid frame
boundary.

A length-based framing format may require transport reset when its boundary
state can no longer be trusted.

Recovery behavior must avoid:

* treating arbitrary bytes as valid protocol messages;
* unlimited scanning or buffering;
* silently combining unrelated frames;
* passing partial or corrupted messages to the deserializer.

Repeated framing failures may cause the Protocol Context or runtime connection
manager to leave Operational state and begin recovery according to ADR-0011.

### Transport profiles

A HASE transport profile defines how framing and transport behavior are
combined for one transport family.

A transport profile may define:

* selected framing mechanism;
* maximum frame size;
* integrity mechanism;
* transport addressing;
* connection assumptions;
* ordering assumptions;
* segmentation and reassembly rules;
* reconnect behavior;
* default timeout guidance;
* transport-specific capability restrictions.

Transport profiles do not redefine the Properties, Commands, Events, message
model, Endpoint Session, or Protocol Context.

Transport profiles adapt the common protocol architecture to one transport
environment.

### Framing selection

The framing mechanism may be:

* fixed by a transport profile;
* selected by configuration;
* negotiated through a future protocol mechanism;
* implicit because the transport already provides suitable message
  boundaries.

Peers must use compatible framing before serialized protocol messages can be
exchanged.

The exact selection or bootstrap mechanism is not defined by this ADR.

Initial protocol negotiation cannot depend on a framing format that has not
already been established by configuration, transport profile, or another
bootstrap mechanism.

### Simulation and loopback transports

Simulation and loopback transports should preserve the same framing boundary
where practical.

Tests may bypass serialization and framing when explicitly testing only the
Protocol Context or higher runtime layers.

Integration tests should include the real framing and serialization pipeline
to verify:

* frame boundaries;
* partial reads;
* combined reads;
* malformed frames;
* oversized frames;
* recovery behavior.

Simulation shortcuts must not change protocol semantics.

### Extensibility

New transports may define new transport profiles without changing the HASE
protocol message model.

New framing formats may be introduced when they preserve:

* one serialized message per frame;
* complete frame reconstruction before deserialization;
* frame-size enforcement;
* clear error boundaries;
* compatibility selection through configuration, profile, negotiation, or
  versioning.

Framing extensions must not expose transport fragments as protocol messages.

## Consequences

### Positive

* Protocol messages remain transport-independent.
* Serialization formats can evolve independently of framing formats.
* Stream-oriented and message-oriented transports use the same message model.
* The Protocol Context can be tested without raw transport data.
* Each frame has one unambiguous semantic payload.
* Large transfers use explicit protocol Streams.
* Transport segmentation remains hidden below protocol semantics.
* Frame-size limits provide a clear resource-protection boundary.
* Transport-specific mappings can evolve through profiles.

### Negative

* Transport integrations require explicit framing infrastructure.
* Stream transports require buffering and boundary reconstruction.
* Constrained transports may require segmentation and reassembly below the
  frame layer.
* Frame-size limits constrain the maximum size of one serialized protocol
  message.
* Each transport profile requires dedicated framing and recovery tests.
* Bootstrap selection of framing and serialization requires later definition.

## Alternatives considered

### Combine framing and serialization

Rejected because message representation and transport boundary detection are
independent concerns.

### Let the Protocol Context process frames

Rejected because protocol execution should remain independent of transport
representation and buffering.

### Allow multiple protocol messages in one frame

Rejected because it complicates partial processing, error handling, message
accounting, correlation, and resource limits.

Transport implementations may still efficiently batch several frames into one
transport write operation without changing frame boundaries.

### Allow one protocol message to span multiple HASE frames

Rejected because it introduces generic protocol-message fragmentation and
complicates correlation, recovery, limits, and error handling.

Large semantic transfers are represented using Streams.

### Treat every transport read as one frame

Rejected because stream-oriented transports do not preserve application
message boundaries and may split or combine data arbitrarily.

### Require one frame to equal one native transport packet

Rejected because constrained transports may need to segment a frame below the
HASE framing boundary.

### Use protocol Streams for transport segmentation

Rejected because protocol Streams represent semantic multi-message transfers,
whereas transport segmentation is an implementation detail below the message
model.

### Define one universal framing format for every transport

Rejected because transports have substantially different boundary, integrity,
payload-size, and delivery characteristics.

A common framing format may still be reused by several transport profiles
where appropriate.

## Relationship to previous ADRs

ADR-0010 defines one protocol message as the semantic unit of protocol
communication.

This ADR defines that one serialized protocol message is carried by exactly
one HASE frame.

ADR-0011 defines how framing and transport failures may affect protocol
lifecycle state.

ADR-0013 defines the Protocol Context, which operates above framing and
serialization.

The concrete serialized representation of protocol messages is defined by
ADR-0015.
