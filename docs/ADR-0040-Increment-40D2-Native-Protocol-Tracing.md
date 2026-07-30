# ADR-0040 Increment 40D2 — Native Protocol V1 Tracing

## Decision

Introduce `NativeRuntimeProtocolDiagnosticConnection`, a decorator around
`IRuntimeProtocolConnection`.

The decorator publishes payload-free Protocol-level request, response, and
failure records through `RuntimeProtocolDiagnosticExchange`. It preserves the
original response instance and rethrows the original exception unchanged.

Native `ProtocolResultResponse` success maps to `Succeeded`; a non-success
result maps to `Failed`; timeout maps to `TimedOut`; cancellation maps to
`Cancelled`; and other exceptions map to `Failed`.

## Metadata boundary

Records carry endpoint identity, `NativeProtocolV1`, logical message type,
protocol correlation identifier, direction, logical serialized payload length,
duration, and stable outcome.

Payload length is derived through the existing native payload codec only while
Protocol tracing is enabled. Metadata extraction failure is isolated and uses
zero as the safe unavailable fallback.

## Capability preservation

The factory returns a decorator with exactly the optional capabilities exposed
by the wrapped connection:

- `IRuntimeProtocolNotificationSource`; and
- `ITransportExchangeTraceSource`.

Subscriptions are forwarded unchanged, preserving existing notification
delivery and aggregate transport-exchange statistics.

`NativeProtocolNotificationDiagnosticObserver` publishes one payload-free
`ProtocolNotificationReceived` record when attached alongside the existing
runtime notification path.

## Privacy boundary

No decoded values, write values, Command arguments, return values, Event
payloads, descriptors, endpoint diagnostic text, exception text, raw payloads,
frame bytes, addresses, ports, credentials, or certificate data are retained.

## Scope boundary

This increment adds the native decorator, notification observer, and unit tests
only. Production connection binding remains unchanged until 40D4.

## Validation

Tests cover successful and unsuccessful native responses, unchanged response
and exception identity, timeout and cancellation classification,
Operational-only silence, exact optional-capability preservation and
forwarding, payload-free notification metadata, and diagnostic-sink isolation.
