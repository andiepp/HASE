# ADR-0040 — Structured Runtime Diagnostics and Tracing

## Status

Accepted; Increments 40A and 40B implemented.

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
3. 40C — Property, Command, and Event interaction diagnostics.
4. 40D — native and compact protocol exchange tracing.
5. 40E — bounded opt-in native and compact byte tracing.
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
