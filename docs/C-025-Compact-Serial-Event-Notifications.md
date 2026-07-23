# C-025 — Compact Serial Event Notifications

## Status

Completed, automated, and physically verified on Windows.

Validation baseline:

```text
.NET solution builds
1,745 automated tests pass
Arduino Uno firmware builds
Physical Arduino Uno compact event delivery verified
Physical Arduino Uno hardware-reset recovery verified
Physical Arduino Uno USB-unplug/replug recovery verified
Protocol Explorer C-025 exits with code 0
```

## Goal

Extend Compact Serial Protocol Version 1 with unsolicited endpoint-to-host event
notifications while preserving the resource-constrained Arduino Uno footprint,
the single-reader connection model, authoritative runtime-host ownership, and the
existing native HASE runtime event abstraction.

C-025 is governed by ADR-0022.

Compact Serial Protocol remains a separate protocol from Protocol Version 1.
The two protocol paths converge only after transport- and protocol-specific
interpretation has produced an existing runtime event identity.

## Physical validation endpoint

The physical endpoint used for C-025 is:

```text
Board                : Arduino Uno
EndpointId           : arduino-uno-01
Descriptor reference : arduino-uno-validation v1
Transport            : USB serial
Baud rate            : 115200
Candidate VID         : 0x2341
Candidate PID         : 0x0043
Controller instrument : arduino-uno-controller-01
Runtime event         : Controller.ButtonPressed
Compact EventId       : 0x01
Event value encoding  : None
```

The validation pushbutton is wired:

```text
Arduino D7 -> pushbutton -> GND
```

Firmware input configuration:

```text
Pin          : D7
Mode         : INPUT_PULLUP
Active level : Low
Debounce     : 50 ms
```

One debounced press produces one event notification. Button release produces no
event.

## Compact unsolicited event frame

C-025 adds the Compact Serial Protocol Version 1 event-notification message type:

```text
Message type   : 0x09 - EventNotification
Correlation ID : 0x00
Payload        : EventId followed by optional event-value bytes
```

For the physical Arduino Uno button event:

```text
Message type   : 0x09
Correlation ID : 0x00
Payload length : 0x01
Payload        : 0x01
```

The payload byte `0x01` is the compact event identifier. This event has no value
bytes.

Correlation identifier zero is reserved for unsolicited notifications.
Request/response exchanges continue to require nonzero correlation identifiers.

A compact event notification with a nonzero correlation identifier is invalid.
A non-event frame with correlation identifier zero is invalid.

The existing compact framing, protocol-version byte, CRC-16/CCITT-FALSE
validation, and maximum frame-size rules remain unchanged.

## One connection reader

Exactly one receive loop owns the compact serial connection.

The receive loop processes every incoming compact frame and separates it by
correlation semantics:

```text
incoming frame
    |
    +-- CorrelationId == 0
    |       |
    |       +-- EventNotification
    |               -> decode unsolicited event
    |
    +-- CorrelationId != 0
            |
            +-- match the one pending correlated request
                    -> complete request/response exchange
```

No competing reader is introduced for events.

This is required because serial transport does not provide independent channels
for correlated responses and unsolicited notifications.

The single-reader rule applies before and after connection replacement.

## Host-side event mapping

Small compact endpoints do not transmit full runtime identities in every event
frame.

The host repository therefore owns the mapping from compact event identifiers to
the existing HASE runtime event identity.

The Arduino Uno registration contains:

```text
Compact EventId : 0x01
InstrumentId    : arduino-uno-controller-01
EventPath       : Controller.ButtonPressed
Encoding        : None
```

The mapping is part of the host-side compact endpoint definition associated with:

```text
arduino-uno-validation v1
```

The compact event identifier is not itself a runtime identity.

The complete identity chain remains:

```text
USB adapter / COM port
    != EndpointId
    != DescriptorReference
    != InstrumentId
    != EventPath
    != Compact EventId
```

Each identity has a distinct purpose.

## Descriptor and runtime event

The host descriptor exposes the event as:

```text
Instrument : arduino-uno-controller-01
Event      : Controller.ButtonPressed
Display    : Button Pressed
```

The compact mapping resolves EventId `0x01` to that descriptor event.

After mapping, compact-specific routing ends. The mapped event is published into
the existing runtime event model:

```text
CompactEventNotification
    ->
CompactEventMapping
    ->
InstrumentId + EventPath
    ->
RuntimeEvent
    ->
RuntimeEventOccurrence
    ->
IRuntimeEventObserver
```

C-025 does not synthesize a Protocol Version 1 `EventNotification`.

Protocol Version 1 and Compact Serial Protocol remain independent wire protocols.

## Event timestamp

The compact event frame intentionally carries no timestamp to preserve the small
endpoint footprint.

The runtime therefore assigns the host observation time in UTC when the mapped
compact notification is routed into `RuntimeEvent`.

Protocol Version 1 continues to preserve its endpoint-provided event timestamp.

For the physical button event:

```text
Runtime value     : null
Timestamp source  : runtime-host observation
Timestamp standard: UTC
```

## Operational authority

Event delivery is tied to the currently authoritative operational compact
connection.

A stable mapped-event source survives physical connection replacement, while the
coordinator explicitly activates and deactivates the physical connection that is
allowed to publish through it.

This provides the lifecycle:

```text
connection A validated and operational
    -> A may publish

connection A faulted/replaced
    -> A loses authority immediately

no authoritative operational connection
    -> no delivery

connection B validated and operational
    -> B becomes the only publisher
```

A stale or replaced connection cannot publish events after authority has moved.

## Readiness boundary

Compact event delivery does not begin merely because a serial port was opened or
bootstrap succeeded.

The operational connection must pass attachment validation and synchronization
before event publication becomes authoritative.

Automated lifecycle coverage confirms that an unsolicited notification arriving
during synchronization is suppressed and is not replayed after the endpoint
reaches `Ready`.

Runtime publication therefore remains readiness-gated consistently with C-024.

## No offline queue and no replay

Compact event notifications are transient occurrences.

C-025 explicitly provides:

```text
Offline event queue : None
Replay on reconnect : Never
```

An event produced while there is no authoritative operational connection is not
retained by the runtime.

Events that occurred on a previous connection are never replayed when a
replacement connection becomes `Ready`.

This behavior is intentional. A runtime event occurrence represents an event
observed through the currently valid endpoint session, not durable endpoint
history.

Persistent event history, if introduced in the future, belongs above this
protocol/runtime-delivery layer.

## Runtime observer continuity

Runtime observers subscribe to the stable `RuntimeEvent`, not to a physical
serial connection.

The observer subscription therefore survives replacement:

```text
RuntimeEvent observer
    |
    +-- connection A
    |      -> event occurrence
    |
    +-- A faulted
    |
    +-- connection B
           -> event occurrence
```

Applications do not unsubscribe and resubscribe merely because the transport
connection was replaced.

This matches the native Protocol Version 1 event-recovery model proven earlier
through C-012 through C-014.

## Shutdown

Shutdown removes compact event authority deterministically.

The runtime host:

1. stops supervision;
2. removes the active compact connection from authoritative event delivery;
3. disposes operational resources;
4. transitions the runtime endpoint to `Disconnected`;
5. removes the attachment from the authoritative inventory and runtime context.

Notifications from a disposed or replaced physical connection cannot reach
runtime observers after shutdown.

## Arduino Uno firmware

The Arduino Uno firmware keeps the event publisher intentionally small.

Added behavior consists of:

- one input pin constant for D7;
- active-low `INPUT_PULLUP`;
- two button-state bytes;
- one debounce timestamp;
- a 50 ms debounce interval;
- `SendButtonPressedEvent()`;
- `PollButton()`;
- one nonblocking `PollButton()` invocation per loop.

The event publisher reuses the existing compact `SendFrame(...)` implementation
and CRC logic.

It does not introduce another persistent frame buffer or an event queue.

Firmware behavior:

```text
stable HIGH
    ->
physical press
    ->
raw LOW
    ->
50 ms stable LOW
    ->
EventNotification 0x09
CorrelationId 0
EventId 0x01
    ->
wait for release before another press can be emitted
```

## Connection supervision and bounded reconnect attempts

Physical C-025 validation exposed an additional lifecycle requirement.

A COM port can remain present while the endpoint processor is reset and produces
no compact protocol response.

Therefore:

```text
COM port present != endpoint responsive
```

Health probing was already bounded. C-025 additionally requires every supervised
compact connection/bootstrap attempt to be bounded.

The supervisor now applies the configured compact probe timeout to each
connection or reconnection attempt.

With the physical default:

```text
Probe interval              : 1 second
Probe timeout               : 3 seconds
Connection/bootstrap timeout: 3 seconds
Reconnect schedule          : immediate, 1 s, 2 s, 5 s, 10 s maximum
```

A silent connection attempt expires and the existing reconnect policy advances
to the next attempt.

Caller cancellation remains distinct from an attempt timeout.

This rule is important for resource-constrained endpoints where a USB-to-serial
adapter can remain visible while the target MCU is unavailable.

## Automated validation

C-025 automated coverage includes:

- compact event frame encoding and decoding;
- reserved zero correlation identifier semantics;
- rejection of invalid correlation/message combinations;
- shared single-reader demultiplexing;
- event delivery before, between, and after correlated responses;
- descriptor-side compact event mappings;
- event-ID resolution to `InstrumentId` and `EventPath`;
- current-connection event authority;
- stale/replaced connection suppression;
- native `RuntimeEvent` publication;
- host-observed UTC timestamps;
- null value for `CompactEventValueEncoding.None`;
- event suppression during synchronization;
- observer continuity across connection replacement;
- no offline event queue;
- no replay after reconnect;
- deterministic shutdown;
- Arduino Uno descriptor registration;
- Protocol Explorer C-025 argument handling;
- bounded reconnect/bootstrap timeout followed by another successful retry.

Final automated baseline:

```text
1,745 tests pass
```

## Protocol Explorer

The physical scenario is:

```text
c025 [baud rate] [verification timeout seconds]
```

Defaults:

```text
Baud rate            : 115200
Verification timeout : 3 seconds
Candidate filter     : VID 0x2341, PID 0x0043
```

C-025 reuses the established C-024 lifecycle:

```text
Windows USB serial discovery
    ->
VID/PID candidate filtering
    ->
temporary authoritative compact bootstrap verification
    ->
explicit selection
    ->
runtime-host inventory attachment
    ->
independent attachment bootstrap
    ->
independent operational connection
    ->
initial synchronization
    ->
Ready
    ->
runtime event observation
```

USB metadata identifies candidates only.
`CompactBootstrapResponse.EndpointId` remains authoritative.

## Physical validation — basic event delivery

Physical validation was completed on Windows with the Arduino Uno discovered as
COM10.

Observed attachment:

```text
Candidate port         : COM10
VID                    : 0x2341
PID                    : 0x0043
Product                : Arduino Uno
Authoritative endpoint : arduino-uno-01
Descriptor reference   : arduino-uno-validation v1
Connection origin      : Discovered
Connection state       : Ready
Inventory entries      : 1
Published endpoints    : 1
```

One physical D7 press produced:

```text
Runtime event    : Button Pressed
Instrument ID    : arduino-uno-controller-01
Event path       : Controller.ButtonPressed
Value            : <null>
Timestamp        : UTC
Occurrence count : 1
```

Orderly shutdown produced:

```text
Detached               : True
Connection state       : Disconnected
Inventory entries      : 0
Published endpoints    : 0
Operational connection : Disposed
Process exit code      : 0
```

## Physical validation — hardware reset with USB present

The Arduino Uno RESET button was held long enough for protocol health probing to
detect the silent endpoint while the USB connection remained physically present.

Observed lifecycle:

```text
Ready
    ->
Faulted
    ->
Connecting
    ->
bounded silent connection attempts
    ->
Connecting
    ->
Synchronizing
    ->
Ready
```

The first physical D7 press produced occurrence 1 before reset.

After recovery:

```text
Observer subscription      : Preserved
Occurrence count after Ready: 1
Replay after reset         : None
```

A second physical D7 press then produced occurrence 2 through the same runtime
event and observer.

This test exposed and verified the bounded connection/bootstrap rule.

## Physical validation — USB unplug/replug

The same committed implementation was then retested by physically removing and
reconnecting the Arduino Uno USB cable.

Observed lifecycle included repeated unavailable-port failures:

```text
Ready -> Faulted
Faulted -> Connecting
Connecting -> Faulted  (COM10 unavailable)
Faulted -> Connecting
Connecting -> Faulted  (COM10 unavailable)
...
Connecting -> Synchronizing
Synchronizing -> Ready
```

Again:

```text
Observer subscription      : Preserved
Occurrence count after Ready: 1
Replay after recovery      : None
Post-recovery D7 occurrence : 2
```

The runtime inventory and publication returned to zero after explicit
detachment, and Protocol Explorer exited with code 0.

## Approved boundaries confirmed

C-025 confirms all ADR-0022 boundaries:

- compact unsolicited endpoint-to-host event frames are implemented;
- correlation identifier zero is reserved for unsolicited notifications;
- one reader owns the compact connection;
- correlated responses and unsolicited events share that reader;
- compact events are mapped through host-side descriptor definitions;
- compact EventId remains distinct from runtime event identity;
- event delivery starts only for the authoritative operational connection;
- stale and replaced connections cannot publish;
- runtime observer subscriptions survive physical connection replacement;
- there is no offline event queue;
- there is no replay after reconnect;
- shutdown stops event delivery deterministically;
- Arduino Uno firmware remains intentionally small;
- Compact Serial Protocol remains separate from Protocol Version 1;
- USB adapter, connection target, endpoint, descriptor, instrument, event path,
  and compact event identities remain distinct;
- a present serial port is not treated as evidence that the endpoint is
  responsive;
- each supervised compact connection/bootstrap attempt is bounded.

## Explicit backlog

C-025 does not introduce:

- persistent event history;
- event replay;
- additional compact event-value encodings;
- multiple compact event sources on the Arduino validation firmware;
- Linux USB serial discovery;
- BLE transport;
- formal compact-profile negotiation;
- northbound runtime-host APIs;
- Tailscale runtime-host discovery.

Those remain separate future capabilities requiring explicit approval.
