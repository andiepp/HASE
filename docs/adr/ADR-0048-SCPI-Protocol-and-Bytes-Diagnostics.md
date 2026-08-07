# ADR-0048 — SCPI Protocol and Bytes Diagnostics

- Status: Implemented, physically validated, and closed at 5,533 tests
- Date: 2026-08-07

## Context

ADR-0044 established the dependency-free serialized SCPI text session and
physically characterized KEL-103 framing. ADR-0045 published the instrument,
ADR-0046 added controlled mutations with explicit uncertain outcomes, and
ADR-0047 added one serialized passive read-only health operation.

The Runtime Host already supported Operational, Protocol, and Bytes diagnostic
levels for Native Protocol V1 and Compact Serial Protocol V1. KEL-103 activity
remained visible only through sanitized Operational records. Diagnosing SCPI
framing, correlation, timeout, cancellation, disposal, and uncertain command
outcomes required equivalent observation without adding a second serial path,
overlapping an exchange, or changing mutation semantics.

## Decision

### Transport-independent observation

`ScpiTextSession` accepts an optional `IScpiDiagnosticObserver`. The existing
constructor remains unchanged and creates no observations. Observation begins
only after the serialized exchange gate is acquired and therefore cannot
interleave two logical exchanges or overlap a second transport path.

Each exchange receives one opaque identifier. Observations describe exchange
start, exact transmitted and received byte chunks, successful completion, or a
sanitized terminal failure. Observer exceptions are isolated from SCPI
execution. Observed byte events own copies and cannot mutate transport buffers.

### Outcome semantics

Queries and Commands remain distinct. Terminal observations retain success,
failure, cancellation, timeout, disposal, and uncertain Command outcome.
Failures expose only a fixed classification. An uncertain Command explicitly
retains whether execution may have occurred. No diagnostic path retries,
replays, or otherwise changes a mutation.

### Runtime disclosure levels

The production KEL-103 observer maps observations into the established runtime
diagnostic model:

- `Operational` publishes no SCPI exchange or byte records.
- `Protocol` publishes correlated, payload-free request and terminal metadata.
- `Bytes` additionally publishes exact transmitted and received snapshots.

Protocol details contain the endpoint identity, `ScpiText` protocol family,
Query or Command message kind, opaque correlation identifier, byte counts,
duration, outcome, fixed failure kind, and uncertain-execution flag where
applicable. They contain no serial-port assignment, SCPI payload, instrument
serial identity, Property value, requested value, credential, deployment
address, or exception message.

Bytes records deliberately represent captured transport bytes. They use the
existing `RuntimeDiagnosticByteSnapshot` limit of 256 captured bytes and retain
the original length and truncation state. The `ScpiText` family discriminator
selects read-only presentation; it does not influence transport execution.

### Production composition and recovery

Each opened production KEL-103 session owns one observer scoped to its
authoritative endpoint identity and existing runtime diagnostic publisher.
Initial synchronization, Property operations, Commands, passive health probes,
and authoritative recovery synchronization all use that same serialized
session. Every recovered physical connection creates a fresh session and
observer. Framing, three-second total timeout, 512-byte response bound,
five-second passive-health interval, recovery policy, and attachment generation
remain unchanged.

### Structured Runtime Host presentation

The existing Runtime Host byte-interpretation service recognizes `ScpiText`.
It presents printable ASCII body, Query/Command/response classification, and
the characterized terminator:

- CR (`0D`) terminates a Query or Command request;
- LF (`0A`) terminates a response.

Missing terminators, empty bodies, unsupported control or non-ASCII bytes,
trailing bytes, and truncated snapshots are reported as malformed or
incomplete. Interpretation is read-only and failure-isolated. Raw captured
bytes remain authoritative.

### Client boundary

The Client Diagnostics window continues to describe Client-side northbound
activity. It does not receive Runtime Host southbound byte snapshots and does
not reconstruct them as Client-captured transport bytes. Remotely projecting
selected Runtime Host diagnostics requires a separate authenticated contract,
disclosure policy, and retention decision.

## Consequences

- Operators can correlate SCPI requests, responses, and terminal outcomes in
  the Runtime Host without invoking an additional instrument operation.
- Bytes capture provides exact bounded evidence while Protocol capture remains
  payload-free.
- Passive health adds no traffic beyond the ADR-0047 schedule; diagnostics only
  observe exchanges that already occur.
- Serialized access, explicit uncertainty, no mutation retry or recovery
  replay, and authoritative synchronization remain unchanged.
- Generic SCPI discovery, arbitrary operator-entered SCPI, VISA, USBTMC, GPIB,
  diagnostic export, and remote Host-diagnostic projection remain separate
  objectives.

## Validation

Automated coverage verifies optional observation, exact byte ownership,
serialization, correlation, level filtering, bounded snapshots, sanitized
failure mapping, explicit uncertain outcomes, production session composition,
default interpreter registration, valid Query/Command/response presentation,
and safe malformed or incomplete interpretation.

Physical validation used the installed Desktop Runtime Host with the KEL-103 in
authoritative CC/OFF state and the external laboratory supply output OFF.
Passive-health and authoritative measurement Property reads produced correlated
Protocol and Bytes records scoped to the KEL-103 endpoint. Transmitted snapshots
ended in `0D`; received snapshots ended in `0A`. Protocol details contained no
payload or deployment-sensitive value. Structured Host presentation classified
the requests as Queries and the replies as responses, agreed with the raw byte
display, and left the endpoint `Ready`.

ADR-0048 closes at 5,533 automated tests passing in Visual Studio 2026 Release
configuration on .NET 10.
