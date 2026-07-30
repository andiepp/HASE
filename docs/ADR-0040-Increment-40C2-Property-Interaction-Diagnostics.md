# ADR-0040 Increment 40C2 — Property Interaction Diagnostics

## Decision

Instrument authoritative northbound Property reads and writes at the
`RuntimeHostPropertyService` boundary.

Each operation publishes an operational start record and exactly one correlated
terminal record:

- `PropertyReadStarted`, `PropertyReadCompleted`, or `PropertyReadFailed`;
- `PropertyWriteStarted`, `PropertyWriteCompleted`, or `PropertyWriteFailed`.

Records carry endpoint identity, attachment generation, instrument identity,
Property identity, operation identity, duration, and stable outcome.
Successful normalized results map to `Succeeded`, normalized timeouts and
`TimeoutException` map to `TimedOut`, cancellation maps to `Cancelled`, and
other failures map to `Failed`.

## Privacy boundary

Property values are never diagnostic fields. This includes requested values,
confirmed values, cached values, `ByteArray` contents, endpoint diagnostic
text, exception messages, stack traces, protocol payloads, and transport bytes.

## Noise boundary

Cached Property queries remain uninstrumented. Therefore cache refresh,
automatic observation, snapshot capture, formatting, and UI polling do not
produce Property interaction records.

## Composition

Northbound composition accepts the runtime's existing diagnostic publisher and
passes it to the Property service. The production desktop host supplies the
publisher owned by its shared runtime context.

## Validation

Tests cover read and write correlation, structural identity, duration, stable
result classification, cancellation, value and diagnostic-text privacy,
cached-query silence, and result-classifier isolation.
