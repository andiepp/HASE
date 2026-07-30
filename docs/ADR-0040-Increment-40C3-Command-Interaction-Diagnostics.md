# ADR-0040 Increment 40C3 — Command Interaction Diagnostics

## Decision

Instrument normalized Command execution at the
`RuntimeHostCommandService.ExecuteAsync` boundary.

Each execution publishes an operational start record and exactly one correlated
terminal record:

- `CommandExecutionStarted`;
- `CommandExecutionCompleted`; or
- `CommandExecutionFailed`.

Records carry endpoint identity, attachment generation, instrument identity,
Command path, operation identity, duration, and stable outcome. Successful
normalized results map to `Succeeded`, normalized timeouts and
`TimeoutException` map to `TimedOut`, cancellation maps to `Cancelled`, and
other failures map to `Failed`.

## Privacy boundary

Command arguments and return values are never diagnostic fields. This includes
`ByteArray` contents, endpoint diagnostic text, exception messages, stack
traces, protocol payloads, and transport bytes.

## Noise boundary

Command validation helpers, attachment operation implementations, automatic
observations, snapshots, formatting, and UI activity are not independently
instrumented.

## Composition

Northbound composition passes its already-composed runtime diagnostic publisher
to both the Property and Command services.

## Validation

Tests cover correlation, structural identity, duration, success, normalized
failure and timeout classification, cancellation, argument and return-value
privacy, diagnostic-text privacy, and unchanged command results.
