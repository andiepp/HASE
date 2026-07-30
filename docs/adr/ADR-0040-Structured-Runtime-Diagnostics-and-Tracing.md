# ADR-0040 — Structured Runtime Diagnostics and Tracing

## Status

Accepted; Increments 40A, 40B, 40C, 40D, and 40E implemented.

## Context

HASE already exposes transport traces, aggregate statistics, runtime connection
diagnostics, operator activity, and application logging. These facilities do not
form one structured path across runtime lifecycle transitions, protocol
exchanges, transport activity, and optional exact bytes.

Troubleshooting physical ESP32 and Arduino endpoints currently requires
correlating several representations. HASE therefore needs one stable diagnostic
vocabulary without changing endpoint authority, runtime decisions, or the
existing logging boundary.

## Decision

`Hase.Runtime` owns a UI-neutral structured diagnostic model and publication
boundary. Records use a common immutable envelope and stable category-specific
details.

Diagnostics are explanatory only. Runtime and client decisions continue to use
their established states, outcomes, and failure categories.

## Levels

Three cumulative levels are defined:

| Level | Purpose | Default |
| --- | --- | --- |
| `Operational` | Attachments, connection, synchronization, recovery, failures, and interaction activity | Enabled |
| `Protocol` | Direction, message kind, correlation, outcome, duration, and payload metadata | Disabled |
| `Bytes` | Immutable bounded snapshots of exact transmitted or received bytes | Disabled |

Protocol tracing is enabled only for diagnostic sessions. Byte tracing requires
explicit local opt-in and cannot be enabled through the current northbound API.

## Record envelope

Each published record contains:

- a UTC timestamp;
- a monotonic process-local sequence number;
- level, category, event name, and severity;
- endpoint identity and attachment generation when known;
- direction and operation identity when relevant;
- duration and outcome when relevant; and
- immutable category-specific details.

Endpoint address, COM port, VID/PID, and other connection metadata never become
endpoint identity.

## Stable categories

- `RuntimeAttachment`
- `RuntimeConnection`
- `RuntimeSynchronization`
- `RuntimeRecovery`
- `RuntimeProperty`
- `RuntimeCommand`
- `RuntimeEvent`
- `ProtocolExchange`
- `TransportBytes`

## Runtime boundary

Producers publish through a runtime-owned abstraction without knowing whether a
record will be displayed, retained, exported in a future increment, or
discarded.

The initial consumers are:

- a null sink; and
- a bounded process-local in-memory collector.

Observer failures must never affect runtime behavior. Producers can query whether
a level is enabled and use lazy event construction so disabled detail does not
incur expensive payload work.

This decision does not replace `ILogger` or the existing aggregate transport
statistics.

## Privacy and security

Structured diagnostics must not contain:

- private keys or certificate contents;
- passwords or credential secrets;
- certificate thumbprints or credential identifiers;
- private-network deployment addresses;
- machine-specific configuration paths; or
- configuration secrets.

Exact protocol bytes may contain application payloads. They are copied
immutably, bounded per record, and collected only through explicit local
enablement.

## Ordering

Diagnostic sequence is process-local. It provides stable ordering within one
publisher and is not a distributed global chronology. UTC timestamps support
human correlation but do not establish distributed causal order.

## Consequences

- Runtime diagnostics gain one stable structured vocabulary.
- Disabled detail levels avoid unnecessary payload construction.
- Diagnostic presentation can evolve independently of runtime producers.
- Endpoint identity remains generation-qualified where applicable.
- Diagnostics cannot change runtime decisions or failure behavior.
- Exact bytes remain an intentional high-detail diagnostic mode.
- Existing logging and aggregate statistics remain valid.

## Implementation

1. 40A — diagnostic domain model, publisher, sinks, bounded collector, and
   tests. **Completed.**
2. 40B — runtime lifecycle diagnostics. **Completed.**
3. 40C — Property, Command, and Event interaction diagnostics. **Completed.**
4. 40D — native and compact protocol exchange tracing. **Completed.**
5. 40E — bounded opt-in native and compact byte tracing. **Completed.**
6. 40F — Desktop Runtime Host presentation.
7. 40G — physical validation, documentation, and closure.

## Implemented lifecycle diagnostics

Increment 40B publishes operational records through the shared runtime and
attachment ownership boundaries.

The runtime context publishes endpoint inventory changes and installs one
connection-status observer for every published endpoint. That observer records:

- attachment start and ready transitions;
- every authoritative connection-state transition;
- synchronization start and successful completion; and
- recovery start plus successful, failed, or cancelled completion.

Both native Protocol V1 and Compact Serial operational graphs decorate their
existing reconnect policy with `RuntimeEndpointReconnectDiagnosticPolicy`.
Every selected retry delay produces one `RecoveryScheduled` record containing
the authoritative endpoint identity, one-based attempt number, zero-based retry
index, and invariant delay in milliseconds. The wrapped policy remains the
source of the delay.

`RuntimeHostAttachmentProjection` remains the authoritative owner of
northbound attachment generation. Committed live projection changes publish
`AttachmentPublished` and `AttachmentEnded` with endpoint identity and the
matching generation. Transport-level recovery records do not import that later
northbound identity.

Observer failures are isolated. Free-form connection status detail, exception
text, addresses, ports, COM names, discovery metadata, certificate data,
credential data, and configuration paths are not copied into lifecycle
records.

Increment 40B is validated with 3,799 passing automated tests.

## Implemented interaction diagnostics

Increment 40C extends operational diagnostics across authoritative Property
reads and writes, normalized Command execution, and runtime Event occurrences.

`RuntimeDiagnosticOperation` publishes one correlated start and terminal pair,
uses one stable operation identifier, measures duration with a monotonic time
source, and publishes at most one terminal record. Successful operations use
`Succeeded`; normalized or exceptional timeouts use `TimedOut`; cancellation
uses `Cancelled`; and other failures use `Failed`. Original results and thrown
exceptions remain unchanged.

`RuntimeHostPropertyService` publishes:

- `PropertyReadStarted`, `PropertyReadCompleted`, and `PropertyReadFailed`; and
- `PropertyWriteStarted`, `PropertyWriteCompleted`, and
  `PropertyWriteFailed`.

`RuntimeHostCommandService` publishes:

- `CommandExecutionStarted`;
- `CommandExecutionCompleted`; and
- `CommandExecutionFailed`.

These records carry endpoint identity, attachment generation, instrument
identity, interaction path, operation identity, duration, and outcome.
Northbound composition passes the runtime context's existing publisher to both
services. Cached Property queries, cache refresh, automatic observation,
snapshot capture, formatting, and UI polling remain silent.

`RuntimeEvent.PublishOccurrence` publishes one `EventOccurred` record before
observer fan-out. It carries endpoint identity, instrument identity, and Event
path. An Event occurrence has no operation identifier, duration, or outcome.
It does not import northbound attachment generation into the runtime Event
model, and multiple runtime observers or northbound subscriptions do not
duplicate the record.

Property values, requested write values, confirmed values, Command arguments,
Command return values, Event payloads, `ByteArray` contents, endpoint diagnostic
text, exception messages, stack traces, protocol payloads, and transport bytes
are excluded from interaction diagnostics.

Increment 40C is validated with 3,823 passing automated tests.

## Implemented Protocol diagnostics

Increment 40D adds payload-free logical Protocol tracing for Native Protocol
Version 1 and Compact Serial Protocol Version 1.

`RuntimeProtocolDiagnosticExchange` publishes:

- `ProtocolRequestSent`;
- `ProtocolResponseReceived`;
- `ProtocolExchangeFailed`; and
- `ProtocolNotificationReceived`.

Records use `RuntimeDiagnosticLevel.Protocol` and
`RuntimeDiagnosticCategory.ProtocolExchange`. They carry the authoritative
runtime endpoint identity, protocol family, logical message kind, protocol
correlation identifier, direction, payload length, and—where applicable—
monotonic duration and outcome.

Native operational bindings are decorated with
`NativeRuntimeProtocolDiagnosticConnection`. One
`NativeProtocolNotificationDiagnosticObserver` follows the coordinator's
replacement-aware notification subscription set. Compact operational
connections are decorated in place with
`CompactRuntimeProtocolDiagnosticConnection` after authoritative bootstrap.
Each Compact physical connection generation owns exactly one
`CompactProtocolNotificationDiagnosticObserver` subscription and removes it
before disposal.

Connection replacement therefore detaches the old diagnostic observer before
attaching the new generation. Existing notification delivery, transport
exchange statistics, connection identity, results, exceptions, and disposal
ownership remain unchanged.

Protocol payloads, decoded values, exception messages, stack traces, addresses,
ports, COM names, configuration paths, credentials, and exact transport bytes
are not Protocol diagnostic fields. Payload length is metadata and is evaluated
only when Protocol diagnostics are enabled. Diagnostic sink failures never
alter protocol behavior.

Compact discovery, verification, and authoritative bootstrap traffic remains
outside attached-runtime Protocol tracing. Exact byte capture remains disabled
and deferred to Increment 40E.

Increment 40D is validated with 3,855 passing automated tests.

Implementation detail is recorded by:

- [40D1 — Protocol Exchange Foundation](../ADR-0040-Increment-40D1-Protocol-Exchange-Foundation.md);
- [40D2 — Native Protocol V1 Tracing](../ADR-0040-Increment-40D2-Native-Protocol-Tracing.md);
- [40D3 — Compact Protocol Tracing](../ADR-0040-Increment-40D3-Compact-Protocol-Tracing.md); and
- [40D4 — Production Protocol Activation](../ADR-0040-Increment-40D4-Production-Protocol-Activation.md).

## Implemented byte diagnostics

Increment 40E adds explicit local opt-in capture of exact complete Native
Protocol Version 1 and Compact Serial Protocol Version 1 frames.

`RuntimeDiagnosticByteSnapshot` owns an immutable bounded copy. It retains at
most 256 bytes per record and preserves the original byte count, captured byte
count, and truncation state. `RuntimeTransportByteDiagnosticPublisher`
publishes these snapshots at `RuntimeDiagnosticLevel.Bytes` in the
`TransportBytes` category. Disabled levels do not invoke the byte factory.

`ProtocolDuplexSession` exposes exact outbound and inbound Native frames through
`ITransportByteTraceSource`. `CompactSerialProtocolConnection` exposes exact
written request frames and exact valid response or notification frames. The
Compact reader creates an owned complete-frame copy only while a byte observer
is subscribed; boot noise and corrupted frame candidates are not published.

Production activation occurs only when the runtime sink enables `Bytes` while
the connection generation is created. Default, Operational-only, and
Protocol-only configurations install no production byte observer and retain
their no-observer receive paths.

Each Native duplex binding owns at most one
`NativeTransportByteDiagnosticObserver` and removes it before stopping its
receive pump. Each Compact operational generation owns at most one
`CompactTransportByteDiagnosticObserver` and removes it before disposing its
inner connection. Replacement therefore replaces rather than duplicates byte
observers. Legacy Native exchange-only transport remains unchanged.

Byte records use the authoritative runtime endpoint identity, protocol family,
direction, and protocol correlation identifier where available. Exact bytes
may contain application payloads, so collection remains bounded, process-local,
disabled by default, and unavailable through the current northbound API.

Increment 40E is validated with 3,884 passing automated tests.

Implementation detail is recorded by:

- [40E1 — Byte Diagnostic Foundation](../ADR-0040-Increment-40E1-Byte-Diagnostic-Foundation.md);
- [40E2 — Native Byte Tracing](../ADR-0040-Increment-40E2-Native-Byte-Tracing.md);
- [40E3 — Compact Byte Tracing](../ADR-0040-Increment-40E3-Compact-Byte-Tracing.md); and
- [40E4 — Production Byte Activation](../ADR-0040-Increment-40E4-Production-Byte-Activation.md).

## Deferred

- persistent diagnostic storage;
- file export;
- northbound diagnostic streaming or remote retrieval;
- remotely enabling byte tracing;
- audit logging;
- automatic trace-on-failure capture;
- protocol payload decoding tools;
- distributed trace propagation;
- OpenTelemetry integration;
- replacement of `ILogger`;
- replacement of aggregate transport statistics; and
- Linux USB discovery.
