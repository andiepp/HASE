# ADR-0040 Increment 40B1 — Shared Runtime Lifecycle Diagnostics

## Status

Implemented; awaiting solution-wide validation.

## Scope

This increment instruments the shared `RuntimeContext` and `RuntimeEndpoint`
status boundary used by both native Protocol V1 and Compact Serial endpoints.

It publishes operational records for:

- endpoint inventory publication and removal;
- attachment start and ready transitions;
- every authoritative connection-state transition;
- synchronization start and completion; and
- recovery start and successful, failed, or cancelled completion.

No coordinator or supervisor is modified in this increment. This prevents the
same state transition from being published independently by the native and
compact lifecycle implementations.

## Runtime construction

`RuntimeContext` accepts an optional `RuntimeDiagnosticPublisher`. Existing
parameterless construction remains source-compatible and uses the null
diagnostic path.

Every endpoint published by the context receives one internal lifecycle
observer. This also covers a compatible endpoint constructed directly and then
published through its owning context. The observer is retained by the
endpoint's existing connection-status subscription and sees the same
authoritative transitions as all other runtime observers.

## Record safety

Connection diagnostics contain:

- endpoint identity;
- previous state; and
- current state.

The existing free-form `EndpointConnectionStatus.Detail` is intentionally not
copied. It may contain exception, transport, address, port, or configuration
information that is outside the approved structured diagnostic contract.

This layer does not own attachment generation, retry attempt, retry delay, or
recovery duration. It therefore does not fabricate those values.

## Verification

Focused tests cover:

- endpoint publication and removal;
- ordered connection, synchronization, and attachment records;
- successful and failed recovery outcomes;
- exclusion of unsafe free-form status detail;
- diagnostic observer isolation; and
- unchanged behavior with the default null diagnostic path.

## Remaining 40B work

Increment 40B2 will instrument the two supervisor implementations for retry
attempt and delay records and the generation-owning host attachment boundary.
