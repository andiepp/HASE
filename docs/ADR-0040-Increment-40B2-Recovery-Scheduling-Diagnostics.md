# ADR-0040 Increment 40B2 — Recovery Scheduling Diagnostics

## Status

Implemented; awaiting solution-wide validation.

## Scope

This increment adds a reusable diagnostic decorator for
`IRuntimeEndpointReconnectPolicy`.

Both native Protocol V1 and Compact Serial endpoint supervisors already obtain
every reconnect delay through this shared contract. The decorator preserves that
contract and adds one structured `RecoveryScheduled` record for each delay
decision.

## Record

The record uses:

- level `Operational`;
- category `RuntimeRecovery`;
- event name `RecoveryScheduled`;
- endpoint identity;
- optional attachment generation; and
- immutable attempt and delay details.

Details are:

| Key | Meaning |
| --- | --- |
| `AttemptNumber` | Human-readable one-based attempt number |
| `RetryIndex` | Exact zero-based input passed to the wrapped policy |
| `DelayMilliseconds` | Invariant millisecond representation of the selected delay |

The record has no outcome because the reconnect attempt has not started when the
policy selects its delay.

## Behavioral compatibility

The wrapped policy is called exactly once and its delay is returned unchanged.
An exception from the wrapped policy propagates unchanged.

Diagnostic sink failures remain isolated by `RuntimeDiagnosticPublisher` and
cannot change the delay or interrupt recovery scheduling.

The decorator contains no exception text, connection address, port, COM name,
certificate information, credential information, or configuration path.

## Composition

The decorator accepts optional attachment generation but does not invent it.
The generation-owning runtime-host attachment boundary will activate the
decorator for both endpoint families in Increment 40B3.

Keeping activation with that boundary prevents transport code from owning
host-publication identity and avoids publishing retry records without the
available generation.

## Verification

Focused tests cover:

- constructor validation;
- the complete default immediate, 1 s, 2 s, 5 s, and capped 10 s schedule;
- one-based attempt and zero-based retry-index mapping;
- invariant millisecond details;
- endpoint identity normalization;
- optional generation preservation;
- disabled diagnostics;
- throwing diagnostic sinks; and
- wrapped-policy exception propagation.
