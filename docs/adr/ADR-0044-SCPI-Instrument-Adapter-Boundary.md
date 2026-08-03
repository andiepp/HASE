# ADR-0044 — SCPI Instrument Adapter Boundary

- Status: Accepted — Increments 44A1 through 44A3 complete
- Date: 2026-08-03

## Context

HASE currently integrates physical endpoints through Native HASE Protocol
Version 1 over framed TCP and Compact Serial Protocol Version 1 over USB serial.
Both families publish the same transport-independent endpoint, instrument,
Property, Command, Event, connection-state, and attachment-generation model.
ADR-0043 completed repeatable deployment and physically validated two
independent Runtime Hosts operating simultaneously through one authenticated
multi-host Client.

The next architectural objective must demonstrate that a commercial instrument
which does not implement either HASE protocol can still participate in the same
runtime and northbound model. SCPI is widely used by programmable laboratory
instruments, but SCPI defines command syntax rather than a universal,
machine-readable HASE descriptor or a single transport and lifecycle model.

The first physical target is a KORAD KEL-103 programmable DC electronic load.
The supplied command reference describes SCPI-style identification, input,
function, setpoint, measurement, limit, storage, trigger, protection, battery,
and dynamic operations. Physical investigation established successful USB
virtual-serial communication at 115200 baud, 8 data bits, no parity, and 1 stop
bit. The read-only `*IDN?` query returned product model, firmware, and
instrument identity information.

The documents do not state the exact command terminator, response terminator,
echo behavior, timeout, maximum response length, or desynchronization recovery
behavior. Increments 44A2 and 44A3 therefore characterize and freeze those
physical facts before reusable production SCPI code is introduced.

## Decision

### Southbound adapter boundary

SCPI is a southbound adapter concern. SCPI command text, abbreviations,
terminators, query matching, units, parsing, instrument-specific error
responses, and transport recovery do not cross into `Hase.Core`, the normalized
runtime-host application services, the gRPC contract, or application-facing
client contracts.

A SCPI instrument is published through the existing HASE endpoint and
instrument descriptor model. Supported capabilities appear as ordinary typed
Properties and Commands. The first capability does not add a SCPI-specific
northbound service or change remote API version 1.

### Runtime Host lifecycle ownership

The Runtime Host exclusively owns:

- the physical serial connection;
- instrument verification;
- descriptor and mapping resolution;
- initial Property synchronization;
- readiness-gated publication;
- serialized operation execution;
- timeout and desynchronization handling;
- health supervision and connection replacement;
- recovery and complete resynchronization;
- explicit detachment; and
- deterministic disposal.

Discovery or configuration never implies automatic attachment or replacement.
The existing authoritative attachment inventory and generation-qualified
operation rules remain unchanged.

### Identity and external configuration

A serial-port target is reachability, not identity. The current machine-specific
COM assignment must remain outside source control and ordinary documentation.

The first adapter uses an explicit versioned host-side KEL-103 definition and
external endpoint configuration. The definition supplies the stable HASE
descriptor and device-specific mappings. External configuration supplies the
physical connection target and serial settings.

Verification uses the read-only `*IDN?` response and an explicit KEL-103
identity policy. The response may contain a per-instrument serial identity.
That identity must not be reproduced unnecessarily in source, examples,
ordinary diagnostics, or validation reports.

### Serialized command/query session

One session owns one physical SCPI connection and permits one ordered exchange
at a time. Concurrent callers cannot interleave command bytes, queries, or
responses.

The session distinguishes:

- a command write, for which no response is expected; and
- a query, for which exactly one bounded response is expected.

A timeout, cancellation, malformed response, unexpected response, or uncertain
stream position must not allow a stale response to satisfy a later query. The
adapter must invalidate or replace a potentially desynchronized connection
before further operations.

### Operation semantics

Read-only queries may be deliberately repeated according to the runtime
synchronization and health policies.

Property writes and Commands retain the existing HASE mutation rules:

- no automatic retry;
- a missing response does not prove that the instrument did not act;
- uncertain outcomes require a later authoritative read where the device
  supports confirmation; and
- values are validated against the authoritative descriptor before formatting
  SCPI text.

Wire formatting and parsing use invariant culture. Unit suffixes and
instrument-specific sentinel values are interpreted only inside the KEL-103
adapter.

### Initial KEL-103 capability slice

The initial mapping is limited to capabilities that fit existing HASE types and
can be validated safely.

Candidate read-only Properties are:

- product/model information from `*IDN?`;
- input state;
- active function;
- configured current;
- measured voltage;
- measured current; and
- measured power.

Candidate writable Properties are input state and constant-current setpoint.
They require separate approval and a reviewed, current-limited physical test
arrangement before state-changing validation.

The exact mappings remain subject to read-only physical characterization. A
command described as query-capable in the vendor document is not published as
an authoritative readable Property until its physical response is confirmed.

### Diagnostics

Operational diagnostics may report the adapter family, operation category,
duration, bounded failure classification, and connection lifecycle without
exposing deployment-sensitive values.

Protocol and byte diagnostics require an explicit sanitization policy. Raw SCPI
text can contain instrument identity or measured data and is not automatically
safe for export or ordinary UI display. Diagnostic observers must not alter
session behavior or operation outcomes.

### First characterization boundary

Increment 44A2 introduced a narrowly bounded characterization utility. Its
command set is compiled-in and read-only. It does not provide an interactive
or arbitrary SCPI console.

The characterization established:

- exact command terminator;
- exact response terminator;
- byte encoding;
- command echo behavior;
- response latency and timeout bound;
- maximum bounded response size;
- close and reopen behavior;
- unsupported or malformed-command recovery where safely testable; and
- whether the documented read-only KEL-103 queries behave as required.

## Consequences

- HASE gains a commercial-instrument adapter family without weakening its
  transport-independent runtime or northbound architecture.
- Existing Desktop Host and Client descriptor-driven presentation can remain
  unaware of SCPI.
- Device-specific command mappings and parsing remain replaceable below the
  runtime model.
- Serial request/response ordering and desynchronization recovery become
  explicit architectural responsibilities.
- Host definitions are required because SCPI does not supply universal
  machine-readable capability discovery.
- The first implementation remains intentionally narrower than generic SCPI or
  VISA support.

## Completed increments

1. 44A1 — SCPI Instrument Adapter Boundary architecture and KEL-103 physical
   starting evidence.
2. 44A2 — Read-Only KEL-103 Serial Characterization utility, automated
   validation, and physical execution.
3. 44A3 — KEL-103 Physical Protocol Characterization documentation.

## Planned increments

1. 44B — Serialized SCPI Text-Session Core.
2. 44C — Versioned KEL-103 Definition and Mappings.
3. 44D — Runtime Attachment, Supervision, and Synchronization.
4. 44E — External Runtime Host Profile Integration.
5. 44F — Existing Desktop Host and Client Presentation.
6. 44G — Physical Multi-Host Validation and Closure.

## Deferred

- arbitrary operator-entered SCPI;
- generic VISA, USBTMC, or GPIB integration;
- automatic SCPI discovery or attachment;
- baud-rate modification by HASE;
- IEEE 488.2 binary blocks;
- service requests and status-byte handling;
- saved configuration and recall;
- external triggering;
- LIST programming;
- OCP and OPP test modes;
- battery-discharge mode;
- dynamic, pulse, and flip modes;
- generalized multi-parameter SCPI operation descriptions;
- a public instrument-definition repository;
- Python automation;
- diagnostic export and offline analysis; and
- remote media streaming.

## Validation state

- ADR-0044 starts from the clean ADR-0043 closure baseline of 4,405 automated
  tests.
- Increment 44A2 adds 31 automated tests; the current verified baseline is
  4,436 passing tests.
- The KEL-103 USB virtual-serial connection is physically verified at 115200
  baud, 8 data bits, no parity, one stop bit, and no flow control.
- The read-only `*IDN?` command is ASCII and is accepted with a CR terminator.
- The physical response is ASCII, ends with LF, and does not echo the command.
- One observed response contained 33 bytes. Its first byte arrived after 4.3 ms,
  and collection completed after 213.6 ms using the configured 200 ms
  post-first-byte idle interval. These timings characterize one run and are not
  production guarantees.
- The returned product family and firmware were recognized while the instrument
  serial identity was redacted.
- The initial physical attempt established that Windows
  `SerialPort.BaseStream.ReadAsync` may remain blocked despite cancellation.
- The corrected utility uses an explicit read-versus-timer race, disposes the
  owned serial port when the timer wins, and has automated coverage for a read
  that ignores cancellation.
- The corrected physical run completed normally, sent no state-changing
  command, and released the port for immediate reuse.
- Increments 44A1 through 44A3 do not change the runtime, remote API, Desktop
  Host, or Client contracts.
