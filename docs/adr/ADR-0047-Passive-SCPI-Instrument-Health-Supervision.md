# ADR-0047 — Passive SCPI Instrument Health Supervision

- Status: Implemented, physically validated, and closed at 5,497 tests
- Date: 2026-08-07

## Context

ADR-0045 introduced the explicitly configured, Runtime-Host-owned KEL-103
attachment and supervised recovery. ADR-0046 added controlled operating state,
setpoints, and input behavior while preserving authoritative readback,
uncertain outcomes, and no automatic mutation retry or recovery replay.

Before this decision, an otherwise idle KEL-103 could remain displayed as
Ready after its USB serial connection was removed. Loss became authoritative
only when the next Property or Command operation attempted serial
communication. Recovery then proceeded correctly, but Host and Client state
did not represent idle transport availability promptly.

Passive detection must not introduce a second SCPI access path, overlap an
operator exchange, replay a mutation, expose raw identity or serial details, or
generate unnecessary traffic.

## Decision

### Fixed read-only health operation

The passive health operation sends exactly one characterized `*IDN?` query
through the existing KEL-103 session. The response must parse as the expected
KEL-103 identity. Returned identity content is not published, cached, or placed
in diagnostics. No alternate query, mutation, automatic retry, or arbitrary
SCPI text belongs to the health path.

### Serialization

The health operation enters through the published connection slot used by
Property reads, Property writes, Commands, and connection replacement. It then
uses the existing serialized SCPI session. These two established gates ensure
that a health probe never overlaps an active exchange or replacement. A queued
probe waits for the current operation and never preempts or duplicates
operator work.

### Schedule

Each supervised KEL-103 attachment owns exactly one passive health monitor.
The monitor:

1. waits five seconds after startup;
2. probes only while the endpoint is `Ready`;
3. waits a complete five-second interval after each probe completes; and
4. never accumulates, catches up, or runs concurrent probes.

There is no immediate startup probe because initial publication already
completed identity verification and authoritative synchronization.

### Failure and recovery

A timeout, unavailable transport, malformed response, or wrong identity faults
the serialized session. The connection slot projects a fixed sanitized
`Faulted` detail. Existing recovery supervision owns replacement and performs
the established identity and read-only authoritative synchronization sequence.

Recovery never repeats or replays a setpoint write, mode selection, input
activation, input deactivation, or confirmed SHORT activation. The Host and
Client require no new contract or presentation feature; existing
connection-state observation carries `Faulted`, `Reconnecting`, and `Ready`.

### Shutdown

Orderly disposal first cancels and awaits the passive monitor, then stops
recovery supervision, then disposes and unpublishes the attachment.
Cancellation requested by this lifecycle is not projected as a communication
failure.

### Diagnostics and privacy

Health failure and recovery diagnostics use fixed sanitized context. They do
not contain serial-port assignments, raw SCPI text, instrument serial identity,
Property or requested values, credentials, deployment addresses, or exception
messages. SCPI Protocol and Bytes diagnostics remain outside this decision.

## Consequences

- Idle KEL-103 USB serial loss becomes authoritative without an operator
  Property or Command operation.
- Normal idle traffic is bounded to at most one characterized read-only query
  per completed five-second interval.
- Existing operation, uncertainty, recovery, attachment-generation, Host,
  Client, and northbound contracts remain unchanged.
- The Runtime Host remains the sole owner of the physical connection lifecycle.
- Automatic discovery, arbitrary SCPI, VISA, USBTMC, GPIB, and energized
  electrical-load validation remain separate objectives.

## Validation

Automated coverage establishes exactly one `*IDN?` per probe, identity
validation without cache mutation, serialized mutation coordination, fixed
delay-before-probe scheduling, Ready-only probing, no catch-up, continued
monitor availability after failure, clean cancellation, and monitor-first
shutdown.

Physical validation used authoritative CC/OFF state with the external
laboratory supply output OFF. With no operator Property or Command operation,
USB removal caused Host and Client to leave `Ready` and enter supervised
recovery. Reconnection returned both to `Ready` through complete authoritative
synchronization. State remained CC/OFF, other endpoints remained operational,
diagnostics remained sanitized, and no mutation was retried or replayed.

ADR-0047 closes at 5,497 automated tests passing in Visual Studio 2026 Release
configuration on .NET 10.
