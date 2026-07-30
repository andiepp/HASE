# ADR-0040 Increment 40D — Protocol Diagnostics Closure

## Status

Complete. Validated with 3,855 passing automated tests.

## Accepted increments

### 40D1 — Protocol exchange foundation

`RuntimeProtocolDiagnosticExchange` established the UI-neutral, payload-free
request, response, failure, and notification vocabulary. Protocol publication
is lazy, cumulative, sink-isolated, and independent of runtime decisions.

See
[40D1 — Protocol Exchange Foundation](ADR-0040-Increment-40D1-Protocol-Exchange-Foundation.md).

### 40D2 — Native Protocol Version 1 tracing

`NativeRuntimeProtocolDiagnosticConnection` added transparent logical exchange
tracing while preserving optional notification and transport-trace capability
shape. `NativeProtocolNotificationDiagnosticObserver` added payload-free
unsolicited notification metadata.

See
[40D2 — Native Protocol V1 Tracing](ADR-0040-Increment-40D2-Native-Protocol-Tracing.md).

### 40D3 — Compact Serial Protocol Version 1 tracing

`CompactRuntimeProtocolDiagnosticConnection` added transparent logical exchange
tracing with lifecycle and optional trace forwarding.
`CompactProtocolNotificationDiagnosticObserver` added payload-free Compact
event-notification metadata.

See
[40D3 — Compact Protocol Tracing](ADR-0040-Increment-40D3-Compact-Protocol-Tracing.md).

### 40D4 — production activation

Native tracing is activated per runtime protocol binding. Native notification
observation follows the coordinator's replacement-aware subscription owner.
Compact tracing is activated per post-bootstrap operational serial connection,
with one observer subscription removed before that connection is disposed.

See
[40D4 — Production Protocol Activation](ADR-0040-Increment-40D4-Production-Protocol-Activation.md).

## Stable record contract

Protocol diagnostics publish:

- `ProtocolRequestSent`;
- `ProtocolResponseReceived`;
- `ProtocolExchangeFailed`; and
- `ProtocolNotificationReceived`.

Stable metadata includes:

- authoritative runtime endpoint identity;
- protocol family;
- logical message kind;
- protocol correlation identifier;
- direction;
- payload length; and
- duration and outcome where applicable.

Protocol payload bytes, decoded values, Property values, Command arguments and
results, Event payloads, endpoint diagnostic text, exception messages, stack
traces, addresses, ports, COM names, credentials, and configuration paths are
excluded.

## Ownership and behavior

Diagnostics remain explanatory:

- original responses are returned unchanged;
- original exceptions are rethrown unchanged;
- diagnostic sink failures cannot affect runtime execution;
- existing notification delivery and transport statistics remain active;
- replacement detaches the old notification observer before attaching the new
  connection generation; and
- no Protocol metadata is constructed when the level is disabled.

Compact discovery, verification, and bootstrap exchanges are not owned by an
attached runtime endpoint and are therefore not emitted as production Protocol
records.

## Remaining ADR-0040 work

- 40E — bounded opt-in native and Compact byte tracing;
- 40F — Desktop Runtime Host diagnostics presentation; and
- 40G — physical validation, final documentation, and ADR-0040 closure.
