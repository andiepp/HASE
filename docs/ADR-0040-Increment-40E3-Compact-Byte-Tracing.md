# ADR-0040 Increment 40E3 — Compact Byte Tracing

## Scope

Add opt-in complete-frame byte tracing to Compact Serial Protocol Version 1
connections without production diagnostic activation.

## Exact inbound frames

`CompactSerialFrameReader.ReadWithBytesAsync` returns one decoded valid frame
and an owned copy of the exact complete bytes consumed from the serial stream.
The existing `ReadAsync` path does not create that copy.

`CompactSerialProtocolConnection` selects the capture path only while at least
one raw-byte observer is subscribed. Boot noise and corrupted candidates remain
reader concerns and are not emitted as valid Compact protocol frames.

## Connection capability

`CompactSerialProtocolConnection` implements `ITransportByteTraceSource`.
It publishes:

- the existing encoded request array after successful serial write;
- each exact correlated response frame; and
- each exact unsolicited event-notification frame.

Correlation identifier zero maps to no correlation metadata. Other Compact
correlation identifiers use invariant decimal text. Duplicate subscriptions
are ignored, observer failures are isolated, and unsubscription removes the
observer.

## Diagnostic observer

`CompactTransportByteDiagnosticObserver` delegates retention to
`RuntimeTransportByteDiagnosticPublisher` with protocol family
`CompactSerialProtocolV1`. The 40E1 256-byte bound, immutable snapshot,
disabled-level silence, original count, captured count, and truncation metadata
remain authoritative.

## Verification

Focused tests cover exact request, response, and notification frames, Compact
correlation metadata, throwing-observer isolation, subscription idempotence and
removal, bounded capture, and Protocol-only silence.

Production subscription remains deferred to Increment 40E4.
