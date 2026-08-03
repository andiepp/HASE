# KEL-103 SCPI Serial Characterization

- Related decision: ADR-0044 — SCPI Instrument Adapter Boundary
- Increment: 44B5C — SCPI Session Migration Closure
- Date: 2026-08-03
- Status: Complete

## Purpose

This report freezes the physical serial and read-only identity behavior observed
for the KORAD KEL-103 programmable DC electronic load before HASE introduces a
reusable production SCPI session.

The characterization is deliberately narrower than runtime attachment. It does
not publish a HASE endpoint, construct descriptors, perform Property operations,
execute a state-changing command, or alter instrument configuration.

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

## Exclusions

This report does not validate:

- input-state queries;
- function queries;
- voltage, current, resistance, or power queries;
- measurements;
- Property writes;
- load input enablement;
- setpoint changes;
- saved configurations;
- triggers;
- LIST, protection, battery, or dynamic modes;
- runtime attachment or supervision;
- reconnection;
- Desktop Host or Client presentation; or
- multi-host operation.

Each requires a later explicitly approved increment.
