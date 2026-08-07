# KEL-103 SCPI Serial Characterization

- Related decisions: ADR-0044 — SCPI Instrument Adapter Boundary; ADR-0045 —
  Runtime-Hosted SCPI Instrument Publication; ADR-0046 — Controlled KEL-103
  Operating State and Setpoints; ADR-0047 — Passive SCPI Instrument Health
  Supervision; ADR-0048 — SCPI Protocol and Bytes Diagnostics
- Increment: 48E — ADR-0048 documentation and closure
- Initial date: 2026-08-03
- Runtime closure date: 2026-08-04
- State-characterization date: 2026-08-04
- Status: Complete

## Purpose

This report freezes the physical serial, read-only identity, measurement, and
Runtime Host behavior observed for the KORAD KEL-103 programmable DC electronic
load.

The original Protocol Explorer characterization is deliberately narrower than
runtime attachment. It does not publish a HASE endpoint, construct descriptors,
perform Property operations, execute a state-changing command, or alter
instrument configuration. Later sections separately record the approved
ADR-0045 production publication and recovery validation.

## Safety boundary

The Protocol Explorer characterization utility:

- accepts one explicit external serial-port target;
- accepts one explicit command terminator;
- opens one owned serial byte stream;
- sends exactly one compiled-in ASCII `*IDN?` query;
- collects one bounded response;
- recognizes product and firmware;
- redacts the returned instrument serial identity;
- closes the owned stream; and
- sends no other SCPI command.

The actual machine-specific serial-port assignment and returned instrument
serial identity are intentionally absent from this report.

## Physical setup

The instrument was connected through its KORAD USB virtual-serial interface.
The vendor application and an independent terminal program had previously
confirmed 115200 baud and successful read-only identity communication.

The HASE characterization used:

| Setting | Value |
| --- | --- |
| Baud rate | 115200 |
| Data bits | 8 |
| Parity | None |
| Stop bits | One |
| Flow control | None |
| Encoding | ASCII |
| Query | `*IDN?` |
| Selected command terminator | CR |
| Total response timeout | 3 seconds |
| Response terminator | LF |
| Maximum response size | 512 bytes |

## Initial timeout finding

The first HASE physical attempt transmitted the read-only query but remained
blocked while awaiting the first response byte. The configured cancellation
timeout did not regain control from
`SerialPort.BaseStream.ReadAsync` on Windows, and the process required operator
interruption.

This did not establish that CR was rejected. It established that serial-read
cancellation alone is not a reliable timeout boundary for this adapter.

The characterization utility was corrected to:

1. start the physical read task;
2. start an independent timer task;
3. await whichever completes first;
4. consume the read result when the read wins;
5. dispose the owned serial port when the timer wins; and
6. observe a later read-task failure without allowing it to affect another
   exchange.

Automated coverage includes a fake read that deliberately ignores cancellation.
The original corrected utility passed all 4,436 automated tests before the
physical probe was repeated. The later production migration replaced idle
collection with deterministic LF framing through `ScpiTextSession`.

## Successful physical result

The corrected CR probe completed successfully:

| Observation | Result |
| --- | --- |
| Response byte count | 33 |
| Response terminator | LF |
| Command echo | Not detected |
| Time to first byte | 4.3 ms |
| Total collection duration | 213.6 ms |
| Product identity | KEL-103 verified |
| Firmware extraction | Succeeded |
| Instrument serial presentation | Redacted |
| State-changing command | None |
| Process completion | Normal |
| Port release | Verified by reopening it from another application |

The measured timings describe this validation run only. They are not protocol
guarantees and must not become hard-coded production expectations.

The total duration is consistent with a prompt response followed by the
configured 200 ms idle interval used to establish that no further byte arrived.

## Migrated SCPI-session validation

The final read-only path uses the transport-independent serialized SCPI session
over the KEL-specific serial byte-stream adapter. It requires one LF-terminated
ASCII frame, rejects echo or trailing data, applies the three-second exchange
timeout and 512-byte bound, and captures first-byte timing in the adapter.

The migrated physical run returned 33 bytes, reached its first byte after
10.2 ms, and completed after 18.7 ms. Product and firmware verification
succeeded, no command echo was detected, the instrument serial remained
redacted, the process exited normally, and an independent terminal application
immediately reopened the port. These timings are observations only.

The validated closure baseline is 4,515 automated tests.

## Frozen physical facts

The initial KEL-103 SCPI serial adapter may rely on these physically established
facts:

- serial framing is 115200 8N1 with no flow control;
- command and response text are ASCII;
- CR is an accepted command terminator;
- the identity response ends with LF;
- the instrument does not echo the identity query;
- `*IDN?` identifies the KEL-103 product family and reports firmware and a
  per-instrument serial identity;
- the serial identity must be redacted from ordinary presentation and
  documentation;
- responses require a bounded maximum size and time policy;
- timeout safety must not depend solely on
  `SerialPort.BaseStream.ReadAsync` cancellation; and
- disposal releases the physical port for another owner.

LF and CRLF were not tested as command terminators because CR succeeded. HASE
must not infer that they are accepted.

## Consequences for Increment 44B

The reusable SCPI text-session core must:

- own one serial stream;
- serialize all exchanges;
- append an explicitly configured command terminator;
- recognize the configured response terminator;
- bound response size and duration;
- distinguish commands from queries;
- invalidate a potentially desynchronized session;
- dispose the physical stream when an exchange cannot be bounded safely;
- prevent stale bytes from satisfying a later query; and
- expose no KEL-103-specific identity or command syntax in its general session
  contracts.

Production timeout and idle values remain decisions for 44B. The
characterization values are safe experimental bounds, not automatically the
production defaults.

## ADR-0045 read-only measurement characterization

Separate bounded Protocol Explorer runs verified the fixed voltage, current,
and power measurement queries before descriptor publication. With the load
input off, voltage followed the connected source while current and power were
zero. With the instrument manually configured for a small constant-current
load, voltage, current, and power returned mutually consistent numeric values
with the exact units V, A, and W. Every run first reverified identity, sent no
state-changing command, closed normally, and released the port for independent
reuse.

The production definition therefore contains product identity, firmware,
measured voltage, measured current, and measured power as read-only Properties.
Invariant parsing, exact units, bounded responses, sentinel rejection, and
identity-before-measurement remain device-specific below the normalized runtime
boundary.

## ADR-0045 runtime and recovery validation

The explicitly configured KEL-103 was published through the production Desktop
Runtime Host beside its existing Arduino and ESP32. Publication occurred only
after identity verification and complete synchronization. Host and Client
showed one electronic-load instrument, five read-only Properties with `Good`
quality, no writable Properties, and no Commands.

Authoritative Client reads succeeded for all five Properties. USB disconnect
and complete instrument power loss were detected by a bounded authoritative
operation. In both cases the published endpoint faulted without disturbing the
Client session or unrelated endpoints, then returned to `Ready` after automatic
connection replacement, identity reverification, and full resynchronization.
The attachment generation remained stable.

Native ESP32 and Compact Arduino Commands, authoritative restoration, and
Events operated concurrently without disturbing the KEL-103. A Laptop Client
also maintained simultaneous authenticated Desktop and MiniPC Runtime Host
sessions; inventory, operations, Events, diagnostics, and independent session
reconnection remained correctly host-scoped. Operational diagnostics exposed
useful sanitized context without serial targets, raw identity responses,
instrument serial identity, SCPI text, Property values, credentials,
configuration paths, or exception text.

Every shutdown released the port for immediate independent reopening and a
redacted read-only identity check. The ADR-0045 closure baseline is 4,772
automated tests passing.

## ADR-0046 read-only operating-state characterization

A separately bounded Protocol Explorer scenario verified identity and then sent
exactly one selected fixed read-only query for operating mode, input state, or
one target. Every exchange used the established 115200 8N1 serial profile, CR
command termination, LF response termination, three-second timeout, and
512-byte response bound. The scenario accepted no arbitrary SCPI text.

Physical mode characterization kept the external supply output off and the
load input off. Mode changes were made only from the instrument front panel.
The exact case-sensitive `:FUNCtion?` responses are:

| Operating mode | Exact response |
| --- | --- |
| Constant current | `CC` |
| Constant voltage | `CV` |
| Constant resistance | `CR` |
| Constant power | `CW` |
| Short circuit | `SHORt` |

Selecting SHORT from the front panel did not activate the input. The input
remained off, the source output remained off, and the original CC mode was
restored and authoritatively reverified afterward.

The exact case-sensitive `:INPut?` responses are `OFF` and `ON`. The ON response
was characterized only after manual front-panel activation while the external
supply output remained off. The input was then manually deactivated and `OFF`
was authoritatively reverified.

The four target queries returned invariant decimal values with the following
exact suffixes:

| Target | Exact suffix |
| --- | --- |
| Voltage | `V` |
| Current | `A` |
| Resistance | `OHM` |
| Power | `W` |

Four fractional digits were observed for every characterized target response.
No target value is retained in this report. All selected state-query durations
in the validation were below five milliseconds, comfortably inside the
three-second bound; observed timings are not protocol guarantees.

Every query first reverified identity, sent no state-changing SCPI command,
closed normally, and released the port. Mode and input changes occurred only
through attended front-panel actions. Final verification confirmed input off,
mode restored to CC, original setpoints unchanged, external supply output off,
and successful independent reopening for a redacted identity-only query. The
validated automated baseline is 4,854 tests passing.

## ADR-0046 read-only setpoint-limit characterization

The first candidate treated `MIN` as a parameter to the ordinary target query.
The fixed `:VOLTage? MIN` request received no framed response and reached the
three-second exchange timeout. The session was disposed, no retry or alternative
request was transmitted during that run, and authoritative follow-up confirmed
that mode, input state, and all four targets were unchanged.

The supported KEL-103 model uses separate `LOW` and `UPP` query paths. A bounded
Protocol Explorer scenario reverified identity and then sent exactly one
selected fixed read-only limit query. The physically established results are:

| Target | Lower query | Lower result | Upper query | Upper result |
| --- | --- | ---: | --- | ---: |
| Voltage | `:VOLT:LOW?` | 0.1000 V | `:VOLT:UPP?` | 120.00 V |
| Current | `:CURR:LOW?` | 0.0000 A | `:CURR:UPP?` | 30.000 A |
| Resistance | `:RES:LOW?` | 0.0500 OHM | `:RES:UPP?` | 7500.0 OHM |
| Power | `:POW:LOW?` | 0.0000 W | `:POW:UPP?` | 300.00 W |

All eight supported responses used invariant decimal text and the same exact
units as their ordinary targets. Returned precision varied by target and bound;
lexical precision is therefore an observed wire-format property rather than a
single universal target precision. Every supported limit query completed in
under five milliseconds during this validation. Timings remain observations,
not protocol guarantees.

The load input and external supply output remained off throughout. Final
identity-gated ordinary queries confirmed CC mode, input off, and all four
original targets unchanged. Every session closed normally, and the port was
independently reopened for a redacted identity-only query. The validated
automated baseline is 4,905 tests passing.

## ADR-0046 input-OFF mode-selection characterization

A bounded Protocol Explorer scenario verified identity, authoritative input
OFF, initial CC mode, and normalized voltage, current, resistance, and power
targets before transmitting one fixed mode-selection command. It required input
to remain OFF and exact destination readback before transmitting one CC
restoration command. It then reverified OFF, CC, and equality of all four target
snapshots. No automatic retry or recovery replay was permitted.

Firmware V3.30 physically established these exact case-sensitive mappings:

| Selection | Setter command | Readback |
| --- | --- | --- |
| CC | `:FUNCtion CC` | `CC` |
| CV | `:FUNCtion CV` | `CV` |
| CR | `:FUNCtion CR` | `CR` |
| CW | `:FUNCtion CW` | `CW` |
| SHORT | `:FUNCtion SHORt` | `SHORt` |

Two candidates were explicitly rejected. `:FUNCtion VOLT` did not change CC to
CV, and `:FUNCtion SHORT` did not change CC to SHORT. Each was transmitted once
without retry. Because destination readback failed, the scenario sent no
restoration command; physical inspection found the instrument already at CC
with input OFF.

The official RND testing package includes a command reference whose legacy
`VOLT`, `CURR`, `RES`, and `POW` vocabulary does not match this firmware's
observed tokens. Static inspection of the supplied command utility found the
mixed-case literal `SHORt` and its `:FUNC %s` format. A separately gated probe
then physically confirmed `:FUNCtion SHORt`.

All four successful destination probes transmitted the destination once and CC
restoration once. Input remained OFF throughout, all four setpoints remained
unchanged, and each connection closed in authoritative CC/OFF state while the
external supply output remained off. SHORT mode selection did not activate the
input and is not evidence for SHORT activation. The validated automated
baseline is 4,937 tests passing.

## ADR-0046 input-OFF setpoint-write characterization

Firmware V3.30 physically established these exact invariant setter forms and
mode side effects:

| Target | Setter form | Resulting mode |
| --- | --- | --- |
| Voltage | `:VOLTage <value>V` | CV |
| Current | `:CURRent <value>A` | CC |
| Resistance | `:RESistance <value>OHM` | CR |
| Power | `:POWer <value>W` | CW |

Each scenario verified identity, input OFF, initial CC, and all four targets
before mutation. Same-value probes transmitted the selected target's current
authoritative value exactly once. The first voltage probe exposed the implicit
CV selection and stopped without an additional command; attended inspection
confirmed CV/OFF and manual CC restoration. The corrected probes confirmed the
expected target mode, input OFF, and unchanged targets, then restored CC once
for voltage, resistance, and power. Current stayed in CC without a redundant
restoration command.

Changed-value probes additionally queried the selected target's established
lower and upper limits. They derived one different candidate at one
response-scale decimal quantum toward the available interior bound. Each
changed setter was transmitted once and confirmed with input OFF, its expected
mode, and all unrelated targets unchanged. The original target was restored
with one setter transmission and complete target readback. Voltage, resistance,
and power then restored CC once; current already remained in CC.

The external supply output remained off throughout. Every successful run closed
in authoritative CC/OFF state with all original targets restored. No mutation
was retried or replayed through a recovery session. Candidate, original,
returned, and bound values remained redacted from output and this report. The
validated automated baseline is 5,000 tests passing.

## ADR-0046 versioned state and controlled-capability definitions

The accepted characterization evidence is represented by two new exact
definition versions under the existing KEL-103 descriptor identity. Existing
versions remain immutable.

Version 3 retains the five version-2 identity and measurement Properties and
adds read-only `Operating.Mode`, `Input.Enabled`, `Target.Voltage`,
`Target.Current`, `Target.Resistance`, and `Target.Power`. The target descriptors
carry the characterized ranges and native units. They intentionally declare no
resolution because response-scale formatting does not establish one invariant
instrument resolution.

Version 4 retains those eleven Properties, makes only the four targets
read/write, and exposes the parameterless Commands
`Mode.SelectConstantCurrent`, `Mode.SelectConstantVoltage`,
`Mode.SelectConstantResistance`, `Mode.SelectConstantPower`, and
`Mode.SelectShortCircuit`. Selecting SHORT does not activate the input.

Through Increment 46F, the production Runtime Host remained on immutable
definition version 2. No runtime mapping, state-changing execution, profile
update, deployment, or migration had yet been performed. The validated
Increment 46F automated baseline was 5,018 tests passing.

## ADR-0046 runtime mode-selection validation

Increment 46G carried the characterized operating-state and setpoint reads,
writes, and five mode Commands through the production runtime and hosting
boundaries. The installed endpoint moved explicitly and offline to definition
version 4. The operation preserved endpoint identity and serial-profile
custody, atomically replaced the active composition, and retained the prior
version-2 composition backup.

The Runtime Host and Client both presented the five descriptor-backed mode
Commands. The accepted Client surface uses direct CC, CV, CR, CW, and SHORT
buttons. A same-generation observation refresh is deferred only for the brief
physical duration of a button activation so one click cannot be lost when the
immutable inventory is reprojected. Host changes, disconnects, faults, and
attachment-generation changes remain immediate.

Physical validation selected CC, CV, CR, CW, and SHORT through the Client with
authoritative input OFF and the external laboratory supply output OFF. Every
accepted press produced one outbound Client Command and one successful
completion, followed by matching displayed and physical mode readback. No
automatic retry or recovery replay occurred. SHORT selection did not activate
the input. One final CC selection restored and confirmed authoritative CC/OFF.
The validated automated baseline is 5,283 tests passing.

## ADR-0046 version-4 recovery and no-replay validation

Increment 46H added automated recovery cases for an uncertain setpoint write
and an uncertain mode Command. Both cases transmit the mutation once, project
the uncertain result as a fault, preserve the published endpoint and operation
ports, and recover through the exact version-4 identity and read-only
synchronization sequence. The replacement session contains no setter or mode
Command, and its authoritative reads replace the cached operating state and all
four targets. Recovery diagnostics retain only sanitized operation metadata.

Increment 46H physical USB-disconnect validation established that the
then-current KEL-103 attachment did not passively probe an otherwise idle
serial connection. The endpoint could therefore remain displayed as Ready
after USB removal until the next Property or Command operation detected the
unavailable transport. Once detected, the session faulted and supervised
replacement proceeded normally. ADR-0047 subsequently closes this known gap.

With USB disconnected, the operator changed the physical mode and one target
while input and the external laboratory supply output remained OFF. On
reconnection the same endpoint and attachment generation returned to Ready,
complete synchronization adopted the manually changed physical state, and no
previous HASE mutation was replayed. All post-recovery reads and explicit mode
Commands succeeded. The run ended in authoritative CC/OFF state with the
external supply output OFF. The validated automated baseline is 5,285 tests
passing.

## ADR-0046 version-5 input-control validation

Increment 46I preserved immutable definition versions 1 through 4 and added
definition version 5. Version 5 retains the complete version-4 inventory and
adds parameterless `Input.Activate` and `Input.Deactivate` Commands plus
`ShortCircuit.Activate` with one required Boolean confirmation argument. Only
normalized `true` authorizes the SHORT activation path; missing, false,
malformed, or unsupported arguments are rejected without SCPI transmission.

The installed endpoint moved explicitly and offline from definition version 4
to version 5. The migration required exact endpoint and definition preflight,
atomically replaced the active composition, retained the version-4 backup, and
preserved endpoint identity, serial target, baud rate, and endpoint count. No
automatic definition migration occurs during startup or recovery.

Ordinary activation verifies authoritative input and mode immediately before
one transmission and rejects SHORT. Confirmed SHORT activation verifies
authoritative SHORT/OFF state immediately before one transmission.
Deactivation has no mode precondition and transmits one OFF command. Every path
requires authoritative input-state readback, exposes uncertain outcomes, makes
no speculative cache update, and is never retried or replayed by recovery.

The Runtime Host and Client both expose dedicated Activate input and Deactivate
input controls. Confirmed SHORT activation is separate from SHORT mode
selection. The Client uses a strict two-state confirmation control whose value
is retained only during connected same-host, same-generation observation
refreshes. It clears after execution and across host, connection, fault, and
attachment-generation boundaries.

Physical validation exercised ordinary activation, deactivation, and
separately confirmed SHORT activation through the Host and Client. Displayed
state and physical instrument state agreed, each accepted mutation produced
one Command execution, and no automatic retry or recovery replay occurred. The
external laboratory supply output remained OFF throughout, and validation
ended in authoritative CC/OFF state. This establishes the control and safety
gates without claiming energized electrical-load performance. The validated
automated baseline is 5,479 tests passing.

## ADR-0047 passive idle health supervision

Increment 47A added one fixed read-only health primitive. It sends exactly one
characterized `*IDN?` query, requires the expected KEL-103 identity, changes no
Property cache, and enters through both the published connection-slot gate and
the serialized SCPI-session gate. It therefore cannot overlap a Property read,
Property write, Command, another probe, or connection replacement.

Increment 47B added exactly one passive monitor to each supervised KEL-103
attachment. It waits five seconds before its first probe, probes only while the
endpoint is Ready, and waits a complete interval after each probe completes.
It performs no catch-up and accumulates no probes. A failure projects fixed
sanitized Faulted state and leaves the established recovery supervisor
responsible for replacement and complete read-only authoritative
synchronization. No mutation is retried or replayed.

Physical validation began in authoritative CC/OFF state with the external
laboratory supply output OFF. USB removal, without any operator Property or
Command operation, caused both Host and Client to leave Ready. Reconnecting the
same USB connection returned both to Ready through complete synchronization.
Authoritative state remained CC/OFF, other endpoints remained operational,
diagnostics remained sanitized, and no state-changing operation occurred.

Orderly disposal stops the passive monitor before recovery supervision and the
published attachment. Lifecycle cancellation is not reported as communication
failure. The validated automated baseline is 5,497 tests passing.

## ADR-0048 SCPI Protocol and Bytes diagnostics

Increment 48A added an optional transport-independent observation boundary to
the serialized SCPI text session. The established constructor remains inactive.
When supplied, the observer receives one opaque exchange identifier, Query or
Command kind, owned copies of exact transmitted and received chunks, duration,
and sanitized terminal outcome. Observation starts only after the session gate
is acquired, cannot overlap another exchange, and cannot affect SCPI execution
if an observer fails.

Increment 48B maps observations into the existing Runtime Host disclosure
levels. Operational capture emits no SCPI Protocol or Bytes records. Protocol
capture emits payload-free endpoint, family, kind, correlation, length,
duration, outcome, and fixed failure metadata. Bytes capture additionally emits
exact snapshots through the established 256-byte capture bound, retaining the
original byte count and truncation status. Explicit uncertain Command outcome
and whether execution may have occurred remain visible without retry or replay.

Increment 48C composes one observer into each production KEL-103 session. The
same serialized path therefore covers initial synchronization, Property reads
and writes, Commands, passive health, and authoritative recovery
synchronization. A recovered connection owns a new session and observer.
Serial framing, total timeout, maximum response, health cadence, replacement,
and attachment-generation behavior remain unchanged.

Increment 48D registers the `ScpiText` family with the Runtime Host structured
byte interpreter. Printable ASCII body, Query/Command/response classification,
and terminator are presented without modifying raw capture. CR (`0D`) denotes a
request; LF (`0A`) denotes a response. Missing or trailing terminators, empty
bodies, unsupported control or non-ASCII bytes, and truncated snapshots are
reported as malformed or incomplete.

Physical validation began and ended with the KEL-103 in authoritative CC/OFF
state and the external laboratory supply output OFF. One passive-health exchange
and one authoritative measurement Property read produced correlated records
scoped to the KEL-103 endpoint. Transmitted snapshots ended in `0D`; received
snapshots ended in `0A`. Protocol details contained no SCPI payload,
machine-specific port assignment, instrument serial identity, or exception
message. Structured Query and response presentation agreed with raw bytes, the
Property read succeeded, and the endpoint remained `Ready`.

The Client Diagnostics window remains scoped to Client-side northbound
activity. Runtime Host southbound SCPI snapshots are not transferred or
reconstructed as Client-captured bytes. The validated automated baseline is
5,533 tests passing.

## Exclusions

This report does not validate:

- resistance measurement publication;
- energized electrical-load performance during input or SHORT activation;
- saved configurations;
- triggers;
- LIST, protection, battery, or dynamic modes;
- authenticated remote projection of Runtime Host southbound diagnostics;
- automatic discovery;
- generic VISA, USBTMC, or GPIB; or
- arbitrary operator-entered SCPI.

Each requires a later explicitly approved increment.
