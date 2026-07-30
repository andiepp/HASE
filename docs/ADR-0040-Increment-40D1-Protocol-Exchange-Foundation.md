# ADR-0040 Increment 40D1 — Protocol Exchange Foundation

## Decision

Introduce `RuntimeProtocolDiagnosticExchange`, a UI-neutral Protocol-level
primitive for correlated logical exchanges and unsolicited notifications.

The primitive publishes:

- `ProtocolRequestSent`;
- `ProtocolResponseReceived`;
- `ProtocolExchangeFailed`; and
- `ProtocolNotificationReceived`.

Records carry endpoint identity, direction, protocol family, logical message
kind, protocol correlation identifier, payload length, duration, and stable
outcome where applicable.

Protocol correlation identifiers remain protocol metadata. They are not
operational `Guid` operation identifiers.

## Level boundary

All records use `RuntimeDiagnosticLevel.Protocol` and
`RuntimeDiagnosticCategory.ProtocolExchange`. The existing cumulative sink
level controls collection. An Operational-only sink does not retain these
records, and lazy publication avoids constructing diagnostic events and detail
dictionaries while Protocol tracing is disabled.

## Privacy boundary

The foundation accepts metadata only. It never accepts decoded values, write
values, Command arguments, return values, Event payloads, descriptors, endpoint
diagnostic text, exception messages, stack traces, raw payloads, frame bytes,
addresses, ports, COM names, credentials, or certificate data.

Payload length is permitted; payload content is not.

## Behavior boundary

Terminal publication is idempotent. Diagnostic sink failures remain isolated.
The primitive does not perform protocol I/O, request matching, timeout,
cancellation, decoding, or retry decisions.

Native and Compact production connections remain unchanged until 40D2 and
40D3.

## Validation

Tests cover request and response metadata, correlation, direction, monotonic
duration, stable success and non-success outcomes, terminal idempotence,
unsolicited notification records, Operational-only silence, and diagnostic-sink
isolation.
