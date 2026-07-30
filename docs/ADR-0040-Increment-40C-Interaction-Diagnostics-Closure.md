# ADR-0040 Increment 40C — Interaction Diagnostics Closure

## Status

Complete.

## Delivered increments

### 40C1 — interaction operation foundation

`RuntimeDiagnosticOperation` provides one UI-neutral correlated operational
scope. It publishes one start and at most one terminal record, preserves one
operation identifier, measures monotonic duration, classifies stable outcomes,
isolates diagnostic failures, and leaves runtime results and exceptions
unchanged.

### 40C2 — Property interaction diagnostics

`RuntimeHostPropertyService` publishes correlated records for authoritative
reads and writes:

- `PropertyReadStarted`, `PropertyReadCompleted`, and `PropertyReadFailed`; and
- `PropertyWriteStarted`, `PropertyWriteCompleted`, and
  `PropertyWriteFailed`.

Cached Property queries remain silent, so cache refresh, observation, snapshot
capture, formatting, and UI polling do not create interaction noise.

### 40C3 — Command interaction diagnostics

`RuntimeHostCommandService` publishes:

- `CommandExecutionStarted`;
- `CommandExecutionCompleted`; and
- `CommandExecutionFailed`.

Normalized results retain their established status and return-value semantics.

### 40C4 — Event occurrence diagnostics

`RuntimeEvent.PublishOccurrence` publishes one `EventOccurred` record before
observer fan-out. Multiple runtime observers or northbound subscriptions do not
duplicate the diagnostic. The runtime Event model remains independent of
northbound attachment generation.

## Correlation and outcomes

Property and Command operations carry endpoint identity, attachment generation,
instrument identity, interaction path, operation identity, monotonic duration,
and stable outcome:

- `Succeeded` for successful normalized results;
- `TimedOut` for normalized or exceptional timeout;
- `Cancelled` for cancellation; and
- `Failed` for other failure.

Event occurrences carry endpoint identity, instrument identity, and Event path.
They are instantaneous facts and therefore have no operation identifier,
duration, or outcome.

## Privacy boundary

Interaction diagnostics never contain:

- Property requested, confirmed, or cached values;
- Command arguments or return values;
- Event values or payloads;
- `ByteArray` contents;
- endpoint diagnostic text;
- exception messages or stack traces;
- protocol payloads; or
- transport bytes.

## Validation

Increment 40C closes with 3,823 automated tests passing.

Coverage includes correlation, monotonic duration, terminal idempotence, stable
outcome classification, cancellation and timeout, unchanged results and
exceptions, structural identity, cached-query silence, one-record Event
fan-out, privacy boundaries, and diagnostic-sink isolation.

## Remaining ADR-0040 work

- 40D — native and Compact Protocol exchange tracing;
- 40E — bounded opt-in native and Compact byte tracing;
- 40F — Desktop Runtime Host diagnostics presentation; and
- 40G — physical validation, documentation, and ADR-0040 closure.
