# ADR-0040 Increment 40D3 — Compact Protocol Tracing

## Scope

Introduce payload-free Protocol-level diagnostics at the Compact Serial
Protocol Version 1 connection boundary. This increment adds no production
activation or user-interface behavior.

## Connection decorator

`CompactRuntimeProtocolDiagnosticConnection` decorates one
`ICompactSerialProtocolConnection`.

When Protocol diagnostics are enabled, every exchange publishes:

- `ProtocolRequestSent` with the Compact message kind, correlation identifier,
  and request payload length;
- `ProtocolResponseReceived` with the response message kind, the same
  correlation identifier, response payload length, outcome, and duration; or
- `ProtocolExchangeFailed` with `TimedOut`, `Cancelled`, or `Failed`.

The decorator returns the original response object and rethrows the original
exception. Diagnostic sinks cannot alter protocol behavior. Payload bytes,
decoded values, exception messages, and endpoint secrets are never recorded.

Message metadata is read only when Protocol diagnostics are enabled. Known
Compact message types use their symbolic names; unknown values use hexadecimal
notation.

## Transparency

The decorator forwards:

- connection state and state-change subscriptions;
- compact event-notification subscriptions;
- invalidation;
- asynchronous disposal.

If the wrapped connection implements `ITransportExchangeTraceSource`, the
created decorator preserves that optional interface and forwards its
subscriptions. A connection without that capability does not acquire it.

## Notifications

`CompactProtocolNotificationDiagnosticObserver` publishes one payload-free
`ProtocolNotificationReceived` record for each decoded Compact event
notification. It records the Compact protocol family, `EventNotification`
message kind, reserved correlation identifier zero, and encoded payload length.

The observer remains separate from the connection decorator so production
subscription ownership can be introduced explicitly in Increment 40D4.

## Verification

Focused unit tests cover:

- successful request/response metadata and response identity;
- timeout, cancellation, and general failure classification;
- disabled-level behavior;
- lifecycle, notification, disposal, and optional trace forwarding;
- payload-free notification metadata; and
- diagnostic-sink isolation.

Production composition and cross-family validation remain deferred to
Increment 40D4.
