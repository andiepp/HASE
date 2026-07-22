# ADR-0022 - Compact Serial Event Notifications

- Status: Accepted
- Date: 2026-07-22

---

# Context

HASE supports Protocol Version 1, Compact Serial Protocol Version 1, framed TCP and USB serial transports, runtime endpoint synchronization, automatic connection recovery, active health probing, runtime event routing, explicit endpoint attachment, and runtime-host-owned attachment inventories.

ADR-0017 established the native Protocol Version 1 duplex model in which one session owns the receive path, correlated responses and unsolicited notifications share that receive path, runtime event subscriptions survive transport replacement, and stale sessions must not continue publishing events.

ADR-0019 established the runtime host as owner of the complete local endpoint communication lifecycle.

ADR-0020 introduced Compact Serial Protocol Version 1 for resource-constrained endpoints. It deliberately kept the compact protocol separate from Protocol Version 1 and reserved correlation identifier zero for future unsolicited notifications. It also required exactly one owner of the compact receive path if unsolicited events were later added.

ADR-0021 established Windows USB serial discovery and authoritative compact bootstrap verification while preserving the separation between connection identity, USB-adapter identity, endpoint identity, and descriptor identity.

C-024 completed the Windows USB serial discovery-to-attachment lifecycle. An explicitly selected or manually configured serial definition converges on one attachment path. Discovery-verification, attachment-bootstrap, and operational connections are distinct. Runtime publication occurs only after authoritative identity validation, descriptor resolution, operational validation, initial property synchronization, and transition to `Ready`. The runtime host owns supervision, recovery, detachment, and shutdown.

The next capability is C-025:

> Deliver unsolicited Compact Serial Protocol event notifications from an attached resource-constrained endpoint into the existing HASE runtime event model, including correct behavior across connection replacement and orderly shutdown.

The first physical target remains the Arduino Uno endpoint.

The compact connection currently performs serialized request/response exchanges. Each exchange writes one request and reads one response. That model is insufficient once the endpoint may transmit an event at any time because an event can arrive while the host is waiting for a correlated response.

Adding another reader would create a race for serial bytes and violate ADR-0020.

C-025 therefore requires a compact duplex receive model while preserving the small endpoint footprint and the existing Compact Serial Protocol Version 1 frame envelope.

---

# Decision

Compact Serial Protocol Version 1 will add one unsolicited event-notification message type.

Correlation identifier `0x00` is reserved exclusively for unsolicited endpoint-to-host notifications.

Every operational compact serial connection will have exactly one receive-loop owner. That reader receives all complete compact frames and dispatches them as either:

1. correlated request/response traffic; or
2. unsolicited event notifications.

Compact event notifications are mapped into the existing HASE runtime event model through host-side descriptor mappings.

The compact protocol remains independent from Protocol Version 1. The two protocols share runtime semantics, not wire messages or transport sessions.

---

# Capability boundary

C-025 includes:

- one Compact Serial Protocol Version 1 unsolicited event-notification message;
- correlation identifier zero semantics;
- one-reader compact connection ownership;
- demultiplexing correlated responses and unsolicited notifications;
- host-side compact event mappings;
- runtime event routing into existing `RuntimeEvent` observers;
- readiness-gated event delivery;
- stale-connection suppression;
- observer continuity across accepted connection replacement;
- no offline queue;
- no replay after reconnect;
- deterministic event-delivery shutdown;
- Arduino Uno firmware support for one physical event;
- automated protocol, routing, lifecycle, recovery, and shutdown tests;
- physical Windows validation through the existing Arduino Uno attachment path.

C-025 does not include:

- event subscription commands sent to the endpoint;
- endpoint-side durable event storage;
- offline event buffering;
- replay or catch-up after reconnect;
- event acknowledgements;
- guaranteed delivery;
- event sequence numbers;
- event batching;
- event priorities;
- compact streaming;
- changes to Protocol Version 1;
- automatic endpoint attachment;
- Linux physical validation;
- BLE;
- remote runtime-host APIs.

Those remain separate future capabilities.

---

# Existing compact frame envelope

C-025 does not change the Compact Serial Protocol Version 1 frame envelope.

Every frame remains:

```text
Offset  Size  Field
------  ----  --------------------------------
0       1     Start marker byte 1 = 0x48
1       1     Start marker byte 2 = 0x53
2       1     Protocol version   = 0x01
3       1     Message type
4       1     Correlation ID
5       1     Payload length N
6       N     Payload
6 + N   2     CRC-16/CCITT-FALSE, big-endian
```

The CRC is calculated over:

```text
ProtocolVersion | MessageType | CorrelationId | PayloadLength | Payload
```

The existing maximum payload length remains 255 bytes.

The existing maximum complete frame length remains 263 bytes.

No framing field is added for C-025.

---

# Compact event message type

Compact Serial Protocol Version 1 assigns:

```text
EventNotification = 0x09
```

The current message-type allocation remains:

```text
0x01  BootstrapRequest
0x02  BootstrapResponse
0x03  ExecuteCommandRequest
0x04  ExecuteCommandResponse
0x05  ReadPropertyRequest
0x06  ReadPropertyResponse
0x07  WritePropertyRequest
0x08  WritePropertyResponse
0x09  EventNotification
```

`EventNotification` is endpoint-to-host only.

The host never sends an `EventNotification` frame to the endpoint.

---

# Correlation identifier rules

Correlation identifier `0x00` is reserved for unsolicited notifications.

Therefore:

- every request uses a nonzero correlation identifier;
- every correlated response uses the same nonzero correlation identifier as its request;
- every `EventNotification` uses correlation identifier `0x00`;
- an `EventNotification` with a nonzero correlation identifier is invalid;
- a request or response using correlation identifier `0x00` is invalid.

The host does not allocate `0x00` for requests.

The endpoint does not use `0x00` for responses.

This creates an unambiguous frame-level distinction without adding another header field.

---

# Compact event payload

An `EventNotification` payload is:

```text
Offset  Size  Field
------  ----  --------------------------------
0       1     Compact Event ID
1       N     Descriptor-defined event value
```

The Compact Event ID:

- is one byte;
- must be nonzero;
- is local to the selected compact endpoint descriptor definition;
- is not an `EventPath`;
- is not an `InstrumentId`;
- is not endpoint identity;
- is resolved only through the host-side compact event mapping associated with the exact descriptor reference established during bootstrap.

The remaining payload bytes are the optional event value.

A mapped event whose descriptor declares no event value uses no value bytes.

For C-025, the physical Arduino Uno event is a no-value event. Its event frame therefore contains exactly one payload byte: the Compact Event ID.

Example logical frame:

```text
Message type   : 0x09
Correlation ID : 0x00
Payload length : 0x01
Payload        : <CompactEventId>
```

The complete encoded bytes are:

```text
48 53 01 09 00 01 <EventId> <CRC-HI> <CRC-LO>
```

The CRC bytes depend on `<EventId>` and are frozen by golden-byte tests when the concrete C-025 event identifier is added.

---

# Event value encoding

Compact event values use descriptor-selected compact encodings.

The endpoint does not transmit `InstrumentId`, `EventPath`, type names, JSON, timestamps, or general-purpose HASE Variant serialization.

The host-side compact event mapping determines:

- the Compact Event ID;
- the target `InstrumentId`;
- the target `EventPath`;
- the expected value encoding;
- whether the event has no value.

Only encodings explicitly supported by the compact profile may be used.

C-025 requires only the no-value form needed by the first Arduino event.

Additional scalar event-value encodings may be added incrementally without changing the frame envelope when they remain backward compatible.

An event whose payload is incompatible with its selected host-side mapping is invalid and is not delivered to runtime observers.

---

# Host-side compact event mappings

The complete endpoint descriptor remains in the runtime-host descriptor repository.

C-025 extends the descriptor-side compact definition with event mappings analogous in purpose to existing compact property and command mappings.

Each compact event mapping binds one nonzero Compact Event ID to exactly one runtime event identity:

```text
Compact Event ID
    -> InstrumentId
    -> EventPath
    -> value encoding
```

Mapping is valid only for the exact descriptor identifier and descriptor version resolved during compact bootstrap.

A compact event identifier must not be guessed from runtime ordering.

A compact event identifier must not be derived from the event path.

A compact event identifier must not be reused ambiguously within one endpoint descriptor definition.

Unknown or ambiguous compact event identifiers are never delivered to runtime observers.

---

# One-reader compact connection model

Exactly one asynchronous receive loop owns `CompactSerialFrameReader.ReadAsync` for an operational compact serial connection.

No request/response operation reads from the serial stream directly.

The receive loop continuously reads complete frames and classifies them.

## Correlated frame

A frame with a nonzero correlation identifier is treated as correlated traffic.

The frame is delivered only to the currently pending exchange with the same correlation identifier.

The existing compact connection may continue to serialize host requests so that at most one request is pending at a time.

C-025 does not require parallel compact requests.

If a nonzero frame cannot be matched to the pending request, the connection is protocol-invalid and is faulted.

## Unsolicited frame

A frame with correlation identifier `0x00` is unsolicited traffic.

For C-025, the only valid unsolicited message type is `EventNotification`.

A zero-correlation frame with another message type is protocol-invalid and faults the connection.

An `EventNotification` with a nonzero correlation identifier is protocol-invalid and faults the connection.

This strict rule prevents accidental confusion between responses and notifications.

---

# Exchange behavior while events arrive

An unsolicited event may arrive:

- while no host request is pending;
- after a request has been written but before its response;
- between two host requests.

The single receive loop consumes the event and continues waiting for subsequent frames.

Receiving an event does not complete, cancel, or alter the pending request.

A later correlated response with the matching nonzero correlation identifier completes that request.

Therefore this sequence is valid:

```text
Host -> Endpoint : ReadPropertyRequest, correlation 0x21
Endpoint -> Host : EventNotification, correlation 0x00
Endpoint -> Host : ReadPropertyResponse, correlation 0x21
```

The event is routed independently and the property exchange completes normally.

---

# Runtime event routing

Compact events enter the existing transport-independent runtime event model.

The compact path must not introduce a second runtime observer abstraction.

For each valid mapped event notification, the host resolves:

```text
Compact Event ID
    -> InstrumentId
    -> EventPath
    -> existing RuntimeEvent
```

The occurrence is then published through the existing runtime event observer mechanism.

Runtime consumers therefore observe the same `RuntimeEvent` model whether the physical endpoint uses Protocol Version 1 or Compact Serial Protocol Version 1.

Wire-protocol differences remain below the runtime event boundary.

---

# Event timestamp

Compact Serial Protocol Version 1 does not add an endpoint timestamp in C-025.

The runtime host assigns the event occurrence timestamp using `DateTimeOffset.UtcNow` when the valid event notification is accepted for runtime delivery.

The timestamp therefore represents host receipt and acceptance time, not guaranteed physical edge time.

This keeps the Arduino firmware and event payload small.

Endpoint-generated event timestamps, clock synchronization, and timestamp-quality semantics require a separate future decision if needed.

---

# Operational validation boundary

Reading frames and delivering runtime events are separate concerns.

The receive loop may need to run as soon as the operational serial connection is created because bootstrap, synchronization, health probes, and normal operations all require correlated response handling.

Runtime event delivery is not enabled merely because bytes can be read.

A compact connection becomes event-delivery eligible only after the attachment lifecycle has successfully completed all required operational validation for that connection:

1. the serial connection is open;
2. Compact Serial Protocol version is accepted;
3. authoritative `EndpointId` is validated;
4. exact descriptor identifier and version are validated;
5. the host-side descriptor and compact mappings are resolved and compatible;
6. required initial property synchronization succeeds;
7. the runtime endpoint is accepted as `Ready`;
8. the connection is the current operational connection owned by the attachment lifecycle.

Before this boundary, unsolicited event frames are consumed but discarded.

They are not queued.

They are not replayed later.

This prevents bootstrap-time, reset-time, and synchronization-time endpoint activity from becoming runtime events before the endpoint is operational.

---

# Connection generation and stale-session suppression

Every operational compact connection participates in the existing coordinator-owned replacement lifecycle.

Only the current accepted operational connection may publish compact events.

A connection that is:

- faulted;
- detached;
- superseded;
- being disposed;
- no longer the coordinator's current connection; or
- not yet operationally activated

must not publish an event occurrence.

Event dispatch therefore requires a current-connection ownership check at the lifecycle boundary, not merely a successfully decoded frame.

When replacement begins, the old connection loses event-delivery authority before it can be treated as stale.

Late frames already in flight from the replaced connection may be consumed during shutdown but are not published after authority has been revoked.

This prevents duplicate or stale event delivery during reconnection races.

---

# Recovery behavior

Compact recovery continues to follow the existing C-021 and C-024 connection-supervision model.

After a transport fault:

- the established runtime endpoint remains;
- cached properties remain available according to existing semantics;
- runtime event observer subscriptions remain attached to the existing runtime model;
- the failed connection loses event-delivery authority;
- the failed connection is closed and replaced according to supervision policy.

The replacement connection repeats the required compact bootstrap and operational validation.

The replacement connection receives event-delivery authority only after identity, descriptor reference, compatibility, synchronization, and `Ready` validation succeed.

Runtime observers do not resubscribe merely because the physical connection changed.

The attachment lifecycle migrates event delivery to the accepted replacement connection.

A different endpoint appearing on the same serial target is not accepted as a replacement and cannot publish events into the existing runtime endpoint.

---

# No offline event queue

Compact events are transient notifications.

The endpoint does not retain an offline event queue for C-025.

The host does not request missed events after reconnect.

The runtime host does not synthesize events for the disconnected interval.

An event that occurs while there is no accepted operational connection is lost.

This includes events occurring:

- while the USB cable is disconnected;
- while the endpoint is rebooting;
- while the host is retrying connection establishment;
- during bootstrap;
- during descriptor validation;
- during initial synchronization;
- before the replacement connection becomes `Ready`.

This behavior matches the native physical event semantics validated by C-013.

---

# No replay after reconnect

After successful reconnection, event delivery resumes only for new unsolicited event frames received through the accepted replacement connection.

Neither the endpoint nor host replays historical events from before the replacement reached operational readiness.

The first post-recovery runtime occurrence must correspond to a new event generated after the replacement connection has become eligible to publish.

This behavior matches the native recovery semantics validated by C-014.

---

# Observer-subscription lifetime

Runtime observer subscriptions belong to the runtime event model, not to one serial connection.

Therefore observer subscriptions survive compact connection replacement for the lifetime of the attached runtime endpoint.

Connection replacement changes only the physical and protocol delivery path.

It does not replace the runtime event object merely to recover transport connectivity.

Explicit endpoint detachment removes the runtime endpoint according to the existing attachment lifecycle. Runtime consumers must not receive further occurrences after detachment completes.

---

# Shutdown

Orderly compact shutdown is deterministic.

Shutdown must:

1. prevent new runtime operations from starting;
2. revoke event-delivery authority from the active compact connection;
3. stop compact supervision and recovery;
4. cancel or complete the active request according to existing connection policy;
5. stop the receive loop;
6. ensure no later receive-loop callback can publish an event;
7. close and dispose the compact protocol connection;
8. close and dispose the serial stream;
9. detach the runtime endpoint from host inventories according to existing attachment ownership;
10. leave the runtime endpoint in `Disconnected` before attachment disposal completes.

After event-delivery authority is revoked, no decoded frame may produce a new runtime event occurrence.

Repeated disposal remains safe.

---

# Failure behavior

The following are protocol failures for an operational compact connection:

- `EventNotification` with nonzero correlation identifier;
- request or response frame with correlation identifier zero;
- zero-correlation message type other than `EventNotification`;
- unmatched nonzero correlated response;
- malformed event payload;
- zero Compact Event ID;
- unsupported event-value encoding when delivery is attempted;
- frame-level corruption already rejected by the compact frame reader.

A protocol failure faults the connection and allows the existing compact supervision lifecycle to perform recovery.

Unknown Compact Event ID handling is intentionally strict for C-025: it indicates that the endpoint and selected host-side compact mapping are incompatible and faults the connection rather than silently ignoring a potentially misrouted physical event.

---

# Arduino Uno footprint

C-025 keeps endpoint-side requirements intentionally small.

The Arduino Uno implementation needs only:

- the existing compact frame encoder;
- one additional message-type constant;
- one nonzero compact event identifier;
- a small fixed event payload;
- the existing CRC implementation;
- the existing serial transmit path;
- physical edge detection and debounce logic appropriate to the selected test input.

The endpoint does not need:

- a complete HASE descriptor;
- runtime paths;
- strings for event identity;
- JSON;
- Variant serialization;
- an event history;
- an acknowledgement table;
- a retransmission queue;
- dynamic memory allocation for event delivery.

C-025 therefore preserves the resource-constrained design goal of ADR-0020.

---

# Identity separation

C-025 preserves all existing identity boundaries.

## Connection identity

The COM port and serial settings identify where the runtime host attempts to communicate.

They are not HASE endpoint identity.

## USB-adapter identity

VID, PID, adapter serial number, product text, manufacturer text, device-instance data, and topology remain optional connection-candidate metadata.

They are not HASE endpoint identity.

## Endpoint identity

`CompactBootstrapResponse.EndpointId` remains authoritative for the physical HASE endpoint.

Event frames do not contain or redefine endpoint identity.

They inherit endpoint context only from the validated operational connection that received them.

## Descriptor identity

Descriptor identifier and version continue to select the exact host-side endpoint definition and compact mappings.

An event frame does not select its descriptor.

## Event identity

Compact Event ID is a compact wire-local identifier resolved through the selected descriptor definition.

It is not an `EventPath` and is not globally authoritative.

The runtime event identity remains the mapped `InstrumentId` plus `EventPath`.

These identities must never be substituted for one another.

---

# Relationship to Protocol Version 1

C-025 does not change Protocol Version 1.

Protocol Version 1 retains its existing `EventNotification` contract and native duplex session implementation.

Compact Serial Protocol Version 1 gains its own compact `EventNotification` wire message with its own message-type allocation, one-byte compact event identifier, correlation-zero rule, and descriptor-side mapping.

The two protocols converge only at the existing HASE runtime event abstraction.

This preserves the frozen boundary established by ADR-0020.

---

# Consequences

## Positive consequences

- Arduino Uno-class endpoints gain unsolicited physical events without adopting Protocol Version 1.
- The compact frame envelope remains unchanged.
- Correlation zero gives an unambiguous notification discriminator at no additional frame cost.
- Exactly one reader owns the serial receive path.
- Events may interleave safely with serialized request/response exchanges.
- Existing runtime event observers are reused.
- Runtime observer subscriptions survive connection replacement.
- Stale or replaced connections cannot publish events.
- Offline events are neither buffered nor replayed.
- Shutdown has a clear event-delivery cutoff.
- Host-side descriptor mappings preserve compact firmware size.
- Connection, USB-adapter, endpoint, descriptor, and event identities remain separate.
- Protocol Version 1 remains unchanged.

## Negative consequences

- The compact protocol connection becomes duplex and therefore more complex than the current synchronous request/read-response implementation.
- Host-side request completion now depends on a continuously running reader.
- Strict handling of unknown event identifiers may fault a connection after descriptor/firmware mismatch.
- Events occurring while disconnected or before operational readiness are intentionally lost.
- Host timestamps represent receipt time rather than endpoint event time.
- Compact event mappings become another compatibility surface in the host descriptor repository.

## Risks

- Incorrect reader ownership could reintroduce competing serial reads.
- A stale receive loop could publish after replacement unless authority is checked explicitly.
- A pending exchange could be completed by the wrong frame unless correlation dispatch remains strict.
- Descriptor and firmware event-ID drift could route physical events incorrectly.
- Shutdown races could deliver an occurrence after detachment unless event authority is revoked before receive-loop disposal.

These risks are controlled through one-reader ownership, correlation rules, exact descriptor resolution, strict mapping validation, lifecycle-gated publication, connection-generation authority, recovery tests, shutdown tests, and physical validation.

---

# Implementation sequence

Implementation proceeds only after this ADR is accepted.

The intended small increments are:

1. freeze compact `EventNotification = 0x09`, correlation-zero rules, event payload codec, and golden-byte tests;
2. add descriptor-side compact event mappings and validation tests;
3. evolve the compact protocol connection to one continuous reader with correlated-response demultiplexing, without runtime event routing;
4. add unsolicited-event dispatch at the compact connection boundary;
5. map compact event notifications to the existing runtime event model;
6. gate delivery on current operational connection ownership and `Ready`;
7. verify replacement migration, stale-session suppression, no offline queue, and no replay;
8. verify deterministic shutdown prevents further delivery;
9. add minimal Arduino Uno event firmware;
10. add Protocol Explorer C-025 physical validation;
11. update capability documentation, project status, and roadmap.

Each increment must remain buildable and testable.

No protocol or runtime implementation begins before ADR-0022 is explicitly accepted.

---

# Physical C-025 validation target

The physical C-025 scenario will use the already attached Arduino Uno-class compact endpoint.

The exact board-side input pin and physical test event identifier are implementation details to be frozen in the first post-ADR increment together with its descriptor mapping and tests.

The physical validation must demonstrate at least:

1. attach the Arduino through the established C-024 path;
2. reach `Ready`;
3. subscribe one existing runtime observer to the mapped runtime event;
4. generate one physical event;
5. observe exactly one runtime occurrence;
6. disconnect or reset the endpoint and verify recovery;
7. confirm no offline event is replayed;
8. return to `Ready`;
9. generate one new physical event;
10. observe exactly one additional occurrence through the same runtime observer subscription;
11. detach or shut down;
12. verify no further event delivery after shutdown begins.

---

# Alternatives considered

## Add a second serial reader dedicated to events

Rejected.

Two readers competing for one byte stream cannot safely determine which reader receives a response or event. This violates ADR-0020's receive-path ownership requirement.

## Keep synchronous `ExchangeAsync` reads and check for events opportunistically

Rejected.

Events can arrive while no request is active, and an event may arrive before a correlated response. Opportunistic reads cannot provide reliable unsolicited delivery.

## Give events normal nonzero correlation identifiers

Rejected.

Unsolicited notifications do not correspond to host requests. Using normal correlation identifiers would blur request/response semantics and require unnecessary allocation or endpoint state.

## Add a new frame flag for unsolicited messages

Rejected.

Correlation identifier zero already exists as a reserved value for this purpose. A new header field would increase every compact frame and change the frozen envelope unnecessarily.

## Put `InstrumentId` and `EventPath` directly on the wire

Rejected.

Strings and full runtime paths defeat the compact endpoint goal. Descriptor-side mappings already provide the correct host translation boundary.

## Queue events in Arduino RAM until the host reconnects

Rejected.

This consumes scarce RAM, introduces overflow and acknowledgement policy, and changes event semantics from transient notifications to reliable messaging.

## Replay events after reconnect

Rejected.

Native physical event behavior already establishes no offline replay. Replaying historical events would also make stale user actions indistinguishable from new post-recovery events.

## Replace runtime event subscriptions on every connection recovery

Rejected.

Subscriptions belong to the transport-independent runtime endpoint and must survive physical connection replacement, matching the native event model.

## Reuse Protocol Version 1 `EventNotification` bytes on serial

Rejected.

Compact Serial Protocol Version 1 is a separate endpoint protocol with different resource constraints and wire contracts. Runtime semantic convergence does not require wire convergence.

---

# Deferred decisions

The following remain outside C-025:

- compact event values beyond the first required no-value event;
- endpoint-originated event timestamps;
- event sequence numbers;
- acknowledgements and retransmission;
- reliable event delivery;
- persistent event queues;
- event batching;
- event priorities;
- host-to-endpoint event subscription negotiation;
- compact streaming notifications;
- formal compact-profile negotiation;
- Linux physical validation;
- BLE transport;
- northbound runtime-host APIs;
- remote access over Tailscale.

These require separate capability selection or architecture decisions when needed.

---

# Acceptance criteria for ADR-0022

ADR-0022 is accepted only if the following decisions are explicit and approved:

- Compact Serial Protocol Version 1 gains unsolicited `EventNotification = 0x09`.
- Correlation identifier zero is reserved exclusively for unsolicited notifications.
- The event payload begins with one nonzero Compact Event ID followed by optional descriptor-defined value bytes.
- Exactly one reader owns the compact serial receive path.
- Correlated responses and unsolicited events share that reader.
- Host requests may remain serialized with at most one pending response.
- Runtime event delivery begins only after operational validation and `Ready`.
- Pre-ready events are consumed and discarded, never queued.
- Offline events are not queued.
- Events are not replayed after reconnect.
- Runtime observer subscriptions survive accepted connection replacement.
- Host-side descriptor mappings translate Compact Event ID into `InstrumentId` and `EventPath`.
- Only the current accepted operational connection may publish events.
- Stale and replaced connections cannot publish.
- Shutdown revokes event-delivery authority before receive-loop disposal and stops delivery deterministically.
- Arduino Uno firmware remains bounded and small.
- Compact Serial Protocol remains separate from Protocol Version 1.
- Connection, USB-adapter, endpoint, descriptor, and event identities remain separate.
