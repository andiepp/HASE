# KEL-103 SCPI Serial Characterization

- Related decisions: ADR-0044 — SCPI Instrument Adapter Boundary; ADR-0045 —
  Runtime-Hosted SCPI Instrument Publication; ADR-0046 — Controlled KEL-103
  Operating State and Setpoints
- Increment: 46B — Read-only mode, input-state, and setpoint characterization
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

## Exclusions

This report does not validate:

- input-state, mode, or target publication;
- resistance measurement publication;
- Property writes;
- load input enablement;
- setpoint changes;
- saved configurations;
- triggers;
- LIST, protection, battery, or dynamic modes;
- SCPI Protocol or Bytes diagnostics;
- automatic discovery;
- generic VISA, USBTMC, or GPIB; or
- arbitrary operator-entered SCPI.

Each requires a later explicitly approved increment.
