# ADR-0040 Increment 40A — Structured Diagnostic Foundation

## Status

Implemented; awaiting solution-wide validation.

## Scope

This increment establishes the UI-neutral structured diagnostic vocabulary and
collection boundary in `Hase.Runtime`.

It adds:

- cumulative `Operational`, `Protocol`, and `Bytes` levels;
- stable diagnostic categories, severities, directions, and outcomes;
- one immutable diagnostic event and record envelope;
- UTC timestamps and process-local monotonic sequence numbers;
- a sink contract;
- a null sink;
- a bounded, thread-safe in-memory collector;
- level and category filtering;
- lazy event construction when a level is disabled; and
- isolation of runtime behavior from diagnostic observer failures.

No existing runtime component emits these records in this increment.

## Identity and ordering

The record envelope can carry endpoint identity and attachment generation.
Connection metadata is not endpoint identity.

Sequence numbers are monotonic only within one publisher instance. They provide
stable process-local presentation order and are not a distributed chronology.
Timestamps are normalized to UTC.

## Diagnostic levels

Levels are cumulative:

| Configured maximum | Enabled records |
| --- | --- |
| `Operational` | `Operational` |
| `Protocol` | `Operational`, `Protocol` |
| `Bytes` | `Operational`, `Protocol`, `Bytes` |

Operational diagnostics are the default collector level. Protocol and exact-byte
records require explicit enablement.

## Collection semantics

`RuntimeDiagnosticPublisher` checks the selected sink before constructing an
expensive diagnostic event. It assigns sequence and timestamp immediately before
publication.

Observer failures are swallowed at the publisher boundary. Diagnostics are
explanatory and must never change runtime behavior.

`BoundedRuntimeDiagnosticCollector` retains only the newest configured number of
records. Snapshots are immutable arrays ordered by sequence and may be filtered
by exact level and category.

## Privacy boundary

The foundation intentionally contains no automatic extraction of connection,
configuration, exception, credential, certificate, or byte-buffer data.
Individual producers introduced by later increments remain responsible for
publishing only approved fields.

The exact-byte level remains disabled unless explicitly selected.

## Verification

Focused automated tests cover:

- value validation and normalization;
- defensive copying of structured details;
- cumulative level enablement;
- bounded retention;
- stable snapshot ordering and filtering;
- UTC timestamp normalization;
- monotonic sequence assignment;
- disabled-level lazy construction;
- observer-failure isolation; and
- null-sink behavior.

Runtime lifecycle instrumentation begins in Increment 40B.
