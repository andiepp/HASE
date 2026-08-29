# ADR-0064 — Serial Transfer Serialization

- Status: Closed; Increment 64C documentation closure
- Date: 2026-08-29
- Starting baseline: `a28fa55ea50f3b2206bf941cc28127f0fb63bfe4`
- Starting subject: `Example 1B: operating the Arduino Uno from the Client on one PC`
- Starting complete Release baseline: 6,516 passed, 0 failed, 0 skipped

## Context

`SystemIoPortsSerialByteStreamFactory` opens a `System.IO.Ports.SerialPort` and
hands its `BaseStream` to the transport as an `ISerialByteStream`. Reads and
writes were passed straight through, so a caller could hold an overlapped read
while issuing a write on the same handle.

Every compact serial connection does exactly that.
`CompactSerialProtocolConnection` runs a continuous receive loop, because
Compact Serial Protocol Version 1 delivers unsolicited Events at any time. The
loop therefore holds a pending read while `ExchangeAsync` writes a request.

ADR-0063 exposed the consequence. The Arduino Uno Light board on AEPRAKETE uses
a CH340 USB-serial adapter. On that adapter, issuing a write while an
overlapped read is pending aborts the read with `ERROR_OPERATION_ABORTED`, and
the stream stays unusable: a retry aborts again. Discovery reports the
candidate as `ConnectionFailed`, verification never completes, and the Runtime
Host reports `NoVerifiedCandidate`.

The behavior was isolated to the adapter, not to the firmware or the
descriptor:

- a direct exchange on the port returns the correct bootstrap response in under
  300 ms, both synchronously and through an asynchronous write-then-read;
- the identical read-pending-then-write sequence fails on the CH340 port and
  succeeds on the official Arduino Uno R3 port of `arduino-uno-01`; and
- it is independent of `DtrEnable`, `RtsEnable`, `FlushAsync`, and of the delay
  between the read and the write.

CH340-class adapters are common on Uno-compatible boards. The Arduino Uno
Compact Endpoint How-To already tells readers that such boards run the same
firmware and need only their own USB identifiers, which is not true today.

## Decision

The owned serial byte stream serializes its transfers. Two measures together
guarantee that no read is outstanding while a write is in progress:

1. **Availability-gated reads.** A read is issued only while the port reports
   buffered bytes through `SerialPort.BytesToRead`. While no bytes are
   buffered, the stream waits and retries instead of leaving an overlapped read
   outstanding.
2. **A transfer gate.** One semaphore makes reads and writes mutually
   exclusive. A read holds the gate only for the transfer itself, which
   completes immediately because bytes are already buffered, so a concurrent
   write is never delayed for longer than one buffered transfer.

`ISystemIoPortsSerialPort` gains `BytesToRead` so the boundary remains
deterministically testable.

The polling interval is 1 ms. The effective wait is the operating-system timer
granularity, approximately 15 ms on Windows, which is the added latency of one
exchange.

### What does not change

- The `ISerialByteStream` contract, the frame codecs, the compact and native
  protocol connections, endpoint identity, verification, attachment, and
  supervision are unchanged.
- The receive loop keeps its shape. It still holds one logical read at all
  times; only the physical overlapped read is now short-lived.
- `StreamSerialByteStream` is unchanged. It adapts an arbitrary stream and has
  no port to ask for buffered bytes.

### Read semantics

A serial port never reaches end of stream, so waiting for buffered bytes is
the correct read semantics for this boundary rather than returning zero. A
physically removed port surfaces its failure from `BytesToRead` or from the
transfer itself, exactly as before, and the existing fault and reconnect paths
handle it.

## Consequences

### Positive

- CH340-class USB serial adapters become usable, which is what the published
  Arduino guidance already promises.
- Reads and writes on one port can no longer interleave at the handle level,
  removing a class of driver-dependent failures rather than one symptom.
- The failure mode it replaces was silent and misleading: a working endpoint
  was reported as `NoVerifiedCandidate`, which reads as absent hardware.

### Negative

- One exchange gains up to one timer tick of latency, approximately 15 ms on
  Windows. Compact Property synchronization of many Properties is
  correspondingly slower.
- The receive loop wakes periodically while idle instead of blocking.

### Neutral

- The change is confined to the owned byte stream created by
  `SystemIoPortsSerialByteStreamFactory`. Every serial endpoint family
  including KEL-103 uses it and inherits the behavior.

## Increment plan

### Increment 64A — Repository application

Goal: serialize transfers in the owned serial byte stream and prove the
boundary with focused tests.

Files modified:

- `src/Hase.Transport/Serial/SystemIoPortsSerialByteStreamFactory.cs`
- `tests/Hase.Transport.Tests/SystemIoPortsSerialByteStreamFactoryTests.cs`
- `tests/Hase.Transport.Tests/SystemIoPortsSerialPortOpenFailureTests.cs`

Files added:

- `docs/adr/ADR-0064-Serial-Transfer-Serialization.md`

Automated validation: focused `Hase.Transport.Tests`, then the complete Release
suite.

Physical or deployment effects: none.

Rollback boundary: the working tree before the increment.

Definition of done: a read waits while no bytes are buffered, a write completes
while a read is waiting, cancellation and disposal behave as before, and the
complete Release suite passes.

Result: 384 passed in the focused `Hase.Transport.Tests` suite, then 6,552
passed, 0 failed, 0 skipped across 28 test projects.

### Increment 64B — Physical revalidation

Goal: confirm both physical serial endpoints on AEPRAKETE under the changed
transport.

Physical or deployment effects: starts the development Runtime Host and Client
against the attached boards. No firmware, deployment, or configuration change.

Definition of done: `arduino-uno-01` still publishes as `Ready` with unchanged
behavior, `arduino-uno-light-01` publishes as `Ready` with both instruments,
and ADR-0063 Increment 63C can be completed.

Result: complete. The Runtime Host publishes `arduino-uno-01`,
`arduino-uno-light-01`, and `simulation-byte-buffer-validation`, all
`Ready`. `arduino-uno-01` behaves as before. The CH340 endpoint verifies,
attaches, synchronizes 20 Properties, accepts an endpoint-confirmed
Property write, executes both Commands, and delivers its Event. Measured
exchange latency on the polled path is approximately 15 ms, as predicted.
ADR-0063 Increment 63C is completed on this result.

### Increment 64C — Documentation closure

Documentation-only closure updates this ADR, `README.md`,
`docs/ProjectStatus.md`, and `docs/Roadmap.md` to a consistent closed state.

## Deferred scope

- Replacing the poll with an event-driven wait for buffered bytes. The
  `SerialPort` data-received event is itself an overlapped operation on the
  same handle and would need its own physical validation on the affected
  adapters.
- Per-port tuning of the polling interval.
