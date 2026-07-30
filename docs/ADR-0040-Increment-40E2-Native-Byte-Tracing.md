# ADR-0040 Increment 40E2 — Native Byte Tracing

## Scope

Add opt-in complete-frame byte tracing to Native Protocol Version 1 duplex
sessions without production activation.

## Raw-frame contract

`ITransportByteTraceSource` and `ITransportByteTraceObserver` define an optional
synchronous transport capability. `TransportByteTrace` carries direction,
callback-scoped read-only frame memory, and an optional correlation identifier.

The source does not copy bytes. Observers that retain bytes own the copy and
must apply their own bound.

## Duplex session

`ProtocolDuplexSession` publishes:

- an outbound trace after a complete encoded request frame is successfully
  handed to the duplex transport; and
- an inbound trace for every complete frame returned by the transport,
  including responses, notifications, and malformed frames.

Inbound correlation extraction is best effort and payload-free. Failure to
extract metadata does not change the existing decoding result or exception.
Observer failures are isolated. No observer means no trace construction or
frame copying.

## Diagnostic observer

`NativeTransportByteDiagnosticObserver` maps raw transport direction into the
runtime diagnostic vocabulary and delegates capture to
`RuntimeTransportByteDiagnosticPublisher`. The 40E1 256-byte bound, immutable
copy, disabled-level behavior, original count, captured count, and truncation
metadata remain authoritative.

## Verification

Focused tests cover exact outbound request frames, inbound response and
notification frames, malformed-frame isolation, throwing observers,
subscription idempotence and removal, bounded capture, and Protocol-only
silence.

Production subscription remains deferred to Increment 40E4. Legacy
request/response transports remain outside this physical Native duplex tracing
increment.
