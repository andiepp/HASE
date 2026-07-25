# C-028 - Northbound Live Observation

## Status

**Completed, automated, and physically verified**

Verified baseline:

```text
2,212 automated tests pass
.NET solution builds
Native Protocol Version 1 physical validation succeeds
Compact Serial Protocol Version 1 physical validation succeeds
Protocol Explorer exits with code 0 for both endpoint families
```

## Purpose

C-028 validates normalized live observation through the public northbound
runtime-host application service while the runtime host remains the sole owner
of every physical endpoint lifecycle.

The capability validates the architecture accepted in ADR-0029. It does not
select or implement a remote wire technology.

## Boundary

The observation subscription is opened before physical endpoint attachment.
Its initial snapshot therefore contains no published endpoints. Subsequent
changes are delivered through one independently buffered subscription.

Every observation contains:

- a subscription-local sequence;
- an authoritative `EndpointId`;
- the opaque attachment generation;
- one immutable normalized payload.

The required physical milestones are:

```text
AttachmentPublished
PropertyValueChanged
EventOccurred
AttachmentEnded
```

Additional authoritative observations may occur between milestones. They are
retained and formatted rather than discarded.

## Frozen Semantics

- The initial snapshot and its sequence form one authoritative boundary.
- Sequences are meaningful only within the subscription that issued them.
- Observation delivery is bounded.
- A buffer gap terminates the subscription explicitly; loss is never silent.
- Every observation is bound to one attachment generation.
- Reattachment with the same endpoint identity receives a new generation.
- Property observations represent values accepted by the runtime Property
  cache.
- Event occurrences remain transient.
- Events have no offline queue and no replay.
- Disposing a subscription does not detach or dispose an endpoint.
- The runtime host owns attachment, connection, synchronization, recovery,
  replacement, detachment, and disposal.

## Protocol Explorer

Commands:

```text
c028 esp32 <host>
c028 arduino [baud rate] [verification timeout seconds]
```

Physical commands used for verification:

```powershell
dotnet run --project .\src\HASE.ProtocolExplorer -- c028 esp32 192.168.0.223
dotnet run --project .\src\HASE.ProtocolExplorer -- c028 arduino
```

The Arduino defaults are:

```text
Baud rate            : 115200
Verification timeout : 00:00:03
Candidate filter     : VID 0x2341, PID 0x0043
```

## Native Protocol Version 1 Validation

Physical endpoint:

```text
Endpoint              : doit-esp32-devkitc-v4-01
Property instrument   : environment-sensor-01
Property              : physical.environment-sensor.temperature
Event instrument      : controller-01
Event                 : Controller.ButtonPressed
Physical input        : GPIO17 pushbutton
```

Observed milestones:

```text
Sequence 1 : AttachmentPublished
Sequence 2 : PropertyValueChanged
Sequence 3 : EventOccurred
Sequence 4 : AttachmentEnded
```

The published attachment was Ready. The authoritative temperature read updated
the runtime Property cache. One physical GPIO17 press produced one
`Controller.ButtonPressed` Event with a null value and UTC timestamp. Orderly
detachment produced `AttachmentEnded`.

All four observations carried endpoint
`doit-esp32-devkitc-v4-01` and one attachment generation.

## Compact Serial Protocol Version 1 Validation

Physical endpoint:

```text
Verified port         : COM10
Endpoint              : arduino-uno-01
Property instrument   : arduino-uno-controller-01
Property              : built-in-led-state
Event instrument      : arduino-uno-controller-01
Event                 : Controller.ButtonPressed
Physical input        : Arduino Uno D7 pushbutton
```

Observed milestones:

```text
Sequence 1 : AttachmentPublished
Sequence 2 : PropertyValueChanged
Sequence 5 : EventOccurred
Sequence 6 : AttachmentEnded
```

Compact synchronization produced additional authoritative
`PropertyValueChanged` observations at sequences 3 and 4 before the Event. The
subscription retained and formatted those intermediate updates. They do not
alter the required milestone order.

One physical D7 press produced one `Controller.ButtonPressed` Event with a null
value and UTC timestamp. Orderly detachment produced `AttachmentEnded`.

All observations carried endpoint `arduino-uno-01` and one attachment
generation.

## Result

C-028 confirms that native Protocol Version 1 and Compact Serial Protocol
Version 1 endpoints use the same transport-independent northbound
live-observation service.

The accepted ADR-0029 boundary is implemented and physically validated:

- snapshot-first subscription establishment;
- generation-bound observation identity;
- lifecycle observation;
- authoritative Property-cache observation;
- transient Event observation;
- permitted retained intermediate updates;
- orderly ending;
- no transfer of endpoint lifecycle ownership.

Phase 7.5 is complete. Remote API mapping, security, and production non-local
exposure remain separate architecture work.
