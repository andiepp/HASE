# ADR-0063 — Arduino Uno Light Endpoint

- Status: Closed; Increment 63D documentation closure
- Date: 2026-08-29
- Starting baseline: `a28fa55ea50f3b2206bf941cc28127f0fb63bfe4`
- Starting subject: `Example 1B: operating the Arduino Uno from the Client on one PC`
- Starting complete Release baseline: 6,516 passed, 0 failed, 0 skipped

## Context

The validated Compact Serial Protocol Version 1 endpoint `arduino-uno-01`
exposes one GPIO controller instrument with a Boolean LED Property, a
parameterless Command, an analog voltage Property, and a push-button Event. It
proves the compact transport but not a compact endpoint that carries real
measurement instruments.

A second Arduino Uno is attached to AEPRAKETE on `COM13`. Its USB serial
adapter reports vendor `0x1A86` and product `0x7523`, so it is distinguishable
from the official Arduino Uno R3 of `arduino-uno-01`, which reports vendor
`0x2341` and product `0x0043`. The board carries two ams-OSRAM light sensors on
its I2C bus:

- an AS7331 UV sensor reporting UV-A, UV-B, and UV-C irradiance; and
- an AS7343 14-channel spectral sensor reporting per-channel acquisition
  counts.

Both sensors are reachable through the published Adafruit Arduino libraries,
which the firmware may depend on because the sensor register maps are not part
of the HASE contract.

Compact Serial Protocol Version 1 currently supports two Property value
encodings: `Boolean` and `Unsigned16LittleEndianMillivolts`. The millivolt
encoding bakes a scale factor into the wire contract and materializes volts, so
it cannot carry an irradiance in µW/cm² or a raw acquisition count. Neither
sensor can be represented without a value encoding that transports an unsigned
16-bit integer in the unit the descriptor declares.

`Hase.Core` also declares no irradiance quantity and no dimensionless count
quantity, so a numeric descriptor for either sensor cannot be authored today.

## Decision

HASE adds a second physical compact endpoint family, `ArduinoUnoLight`, with a
new descriptor and a separate Arduino application. The existing
`arduino-uno-validation` descriptor and its firmware remain unchanged.

### Identity

```text
EndpointId        arduino-uno-light-01
DescriptorId      arduino-uno-light
DescriptorVersion 1
InstrumentId      arduino-uno-light-uv-01
InstrumentId      arduino-uno-light-spectral-01
```

The endpoint publishes two instruments because the board carries two physically
distinct sensors with independent acquisition, availability, and failure
behavior. Collapsing them into one instrument would misreport which sensor is
degraded.

### Compact value encoding

Compact Serial Protocol Version 1 gains one Property value encoding:

`CompactPropertyValueEncoding.Unsigned16LittleEndian` (`0x03`) transports an
unsigned 16-bit little-endian integer and materializes it as a `double` in the
unit declared by the descriptor. The wire value carries no implicit scale
factor. The encoder accepts a finite `double` in the closed interval
`[0, 65535]`, rounds half away from zero, and rejects every other value. It
does not clamp.

This is a purely additive change to the frozen Version 1 frame layout: no
existing message type, status, or encoding is reinterpreted.

### Core quantities and units

`Hase.Core` gains the quantities `irradiance` and `count`, together with the
units `microwatt-per-square-centimetre` (`µW/cm²`) and `count` (`counts`). Both
flow through the existing generic northbound descriptor mapping without a
contract change.

### Capability surface

The AS7331 instrument exposes:

| Compact ID | Capability | Type and access |
| --- | --- | --- |
| Property `0x01` | `Uv/A` | Irradiance in µW/cm², read |
| Property `0x02` | `Uv/B` | Irradiance in µW/cm², read |
| Property `0x03` | `Uv/C` | Irradiance in µW/cm², read |
| Property `0x04` | `Uv/AlarmThreshold` | Irradiance in µW/cm², read/write |
| Property `0x05` | `Uv/SensorReady` | Boolean, read |
| Command `0x01` | `Uv/Measure` | Parameterless Command |
| Event `0x01` | `Uv/AlarmRaised` | Null-payload Event |

The AS7343 instrument exposes Properties `0x10` through `0x1D` as the read-only
channels `Spectral/F1`, `F2`, `FZ`, `F3`, `F4`, `F5`, `FY`, `FXL`, `F6`, `F7`,
`F8`, `NIR`, `VisibleTopLeft`, and `VisibleBottomRight` in counts, Property
`0x1E` as the Boolean `Spectral/SensorReady`, and Command `0x02` as
`Spectral/Measure`.

Descriptor ranges are `0` to `65535` with resolution `1` because the wire
carries an unsigned 16-bit integer. An irradiance above the declared maximum is
reported as the declared maximum.

### Firmware measurement model

The firmware refreshes one coherent snapshot of both sensors every 500 ms.
Property reads return that snapshot, so a read never triggers a conversion and
never blocks the transport for the duration of an acquisition. The two Measure
Commands refresh the snapshot of one sensor immediately and report
`ExecutionFailed` when that acquisition fails.

A Property read of a sensor channel returns `ReadFailed` while its sensor was
not initialized or its last acquisition failed. The two sensor-ready Properties
remain readable in both cases, so an endpoint with one missing sensor still
attaches, still publishes, and reports which sensor is unavailable. This
follows the existing runtime behavior: compact Property synchronization
tolerates a non-success read and leaves the cached value unset.

The UV-A alarm is edge-triggered. The endpoint publishes `Uv/AlarmRaised` once
when the measured UV-A irradiance rises above `Uv/AlarmThreshold`, and rearms
only after the reading falls back below the threshold by a hysteresis of
`threshold / 16 + 1`. A threshold of zero disables the alarm. Events are not
queued or replayed while disconnected.

### Registration and composition

`ProductionPrivateNetworkRuntimeHostBackend` registers the new definition
beside the two existing Arduino definitions in the immutable compact definition
repository. The endpoint is attached only where an endpoint-composition profile
declares a `CompactSerial` entry for `arduino-uno-light-01` with the board's
actual vendor and product identifiers. Endpoint composition remains
configuration, not code.

Because the two boards report different USB vendor and product identifiers, the
existing single-verified-candidate selection in `AttachCompactEndpointAsync` is
sufficient. The same-VID/PID multi-board selection boundary described in the
Arduino Uno Compact Endpoint How-To remains unimplemented and out of scope.

### Client presentation

The WPF Client is descriptor-driven. It renders the endpoint, both instruments,
every Property with its declared unit, the writable threshold editor, both
Commands, and the Event feed from the published descriptor alone. ADR-0063
introduces no endpoint-specific Client code.

## Consequences

### Positive

- HASE gains a compact endpoint carrying real measurement instruments rather
  than transport validation affordances only.
- The compact boundary gains a general unsigned 16-bit Property encoding usable
  by any future endpoint whose descriptor declares the unit.
- Two instruments on one endpoint exercise the multi-instrument model against
  physical hardware.
- Per-sensor readiness Properties and `ReadFailed` reads make a partially
  wired board diagnosable through the normal model instead of through firmware
  logs, which the compact transport forbids.

### Negative

- The firmware depends on two external Arduino libraries, so its build is no
  longer self-contained like `HaseArduinoUno`.
- An unsigned 16-bit wire value fixes the reported resolution at one unit. UV
  readings below 1 µW/cm² are reported as zero.
- A blocking spectral acquisition of roughly 50 ms delays request processing
  for that interval once per measurement period.

### Neutral

- The existing `arduino-uno-01` endpoint, its descriptor versions, and its
  firmware are untouched.
- The new quantities and units are additive; no existing descriptor changes.

## Increment plan

### Increment 63A — Repository application

Goal: implement the encoding, the core quantities and units, the host-side
definition, the Arduino application, and their focused tests.

Files added:

- `HaseArduinoUnoLight/HaseArduinoUnoLight.ino`
- `src/Hase.DesktopHost.App/Physical/ArduinoUnoLightCompactDefinitionFactory.cs`
- `tests/Hase.DesktopHost.Tests/ArduinoUnoLightCompactDefinitionFactoryTests.cs`
- `docs/adr/ADR-0063-Arduino-Uno-Light-Endpoint.md`

Files modified:

- `src/Hase.Core/Domain/Data/Quantities.cs`
- `src/Hase.Core/Domain/Data/Units.cs`
- `src/Hase.CompactProtocol/CompactPropertyValueEncoding.cs`
- `src/Hase.CompactProtocol/CompactPropertyValueDecoder.cs`
- `src/Hase.CompactProtocol/CompactPropertyValueEncoder.cs`
- `src/Hase.DesktopHost.App/Hosting/ProductionPrivateNetworkRuntimeHostBackend.cs`
- `tests/Hase.CompactProtocol.Tests/CompactPropertyValueDecoderTests.cs`
- `tests/Hase.CompactProtocol.Tests/CompactPropertyValueEncoderTests.cs`
- `docs/Arduino-Uno-Compact-Endpoint-How-To.md`

Automated validation: focused `Hase.CompactProtocol.Tests` and
`Hase.DesktopHost.Tests`, then the complete Release suite.

Physical or deployment effects: none.

Rollback boundary: the working tree before the increment.

Definition of done: the complete Release suite passes with no new failures,
the changed-path set matches the list above, and the Arduino sketch is not yet
compiled or uploaded.

Result: 6,548 passed, 0 failed, 0 skipped across 28 test projects, from the
6,516-test starting baseline. `README.md`, `docs/ProjectStatus.md`, and
`docs/Roadmap.md` are left to Increment 63D, because the endpoint is not
physically validated at this point.

### Increment 63B — Firmware compilation

Goal: prove the Arduino application compiles for `arduino:avr:uno` with the two
Adafruit libraries installed, without uploading.

Physical or deployment effects: installs the Arduino libraries
`Adafruit AS7331 Library` and `Adafruit AS7343` with their `Adafruit BusIO`
dependency into the Arduino sketchbook of the operating computer. No device is
written.

Definition of done: `arduino-cli compile` reports success and the reported
program and dynamic memory usage fit the ATmega328P.

Result: `Adafruit AS7331 Library@1.0.1` and `Adafruit AS7343@1.1.0` installed
beside the already present `Adafruit BusIO@1.17.4`. `arduino-cli 1.3.1`
compiled the sketch for `arduino:avr:uno`: 12,722 bytes of program storage
(39 percent of 32,256) and 625 bytes of dynamic memory (30 percent of 2,048).
The installed library headers were read before compilation to confirm every
called member.

### Increment 63C — Controlled upload and physical validation

Goal: upload the application to the board on `COM13`, add the endpoint to the
AEPRAKETE development endpoint composition, and validate the endpoint from the
Runtime Host and the Client on that computer.

Physical or deployment effects: one firmware upload to the attached board and
one edit of the development endpoint-composition profile. Both are separately
approved. A successful compilation is not upload authorization.

Definition of done: the endpoint publishes as `Ready` with both instruments,
each readable Property reports a plausible value or a scoped failure, both
Commands execute, a threshold write is confirmed by the endpoint, the alarm
Event is observed once per crossing, and disconnect and reconnect recover
without operator intervention.

Result: blocked on a transport defect unrelated to this endpoint. The upload
succeeded and the composition entry was added, but the endpoint does not
attach. The Runtime Host reports `EndpointStartupUnavailable` with
`FailureCategory = NoVerifiedCandidate`, and repeated discovery reports the
candidate on `COM13` as `ConnectionFailed`: "The I/O operation has been
aborted because of either a thread exit or an application request."

The firmware is not the cause. A direct serial exchange on `COM13` returns the
correct bootstrap response - endpoint `arduino-uno-light-01`, descriptor
`arduino-uno-light` version 1 - in under 300 ms, both synchronously and
through an asynchronous write-then-read on `SerialPort.BaseStream`.

The cause is the board's CH340 USB-serial adapter. Issuing a write while an
overlapped read is pending on the same `SerialPort.BaseStream` aborts that
read with `ERROR_OPERATION_ABORTED`, and the stream stays poisoned: a retry
aborts again. The same sequence on the official Arduino Uno R3 of
`arduino-uno-01` completes normally. The behavior is independent of `DtrEnable`,
`RtsEnable`, `FlushAsync`, and of the delay between the read and the write.

`CompactSerialProtocolConnection` keeps exactly that shape: the receive loop
holds a pending read while `ExchangeAsync` writes the request. Every
CH340-class adapter therefore fails compact verification today, whatever
firmware it runs. Supporting them requires a separately approved transport
increment. ADR-0064 takes that decision: the owned serial byte stream
serializes its transfers, reading only while the port reports buffered
bytes and keeping reads and writes mutually exclusive. Increment 63C
resumes after ADR-0064 Increment 64B revalidates both boards.

Until that decision, the composition entry for `arduino-uno-light-01` remains
in the AEPRAKETE development profile and produces one scoped
`EndpointStartupUnavailable` warning per Runtime Host start. It does not
affect `arduino-uno-01`, the simulation endpoint, or Runtime Host readiness.

Resolution and result: complete. Under ADR-0064 the endpoint attaches and was
validated from the Runtime Host and the Client on AEPRAKETE:

- the Runtime Host publishes three endpoints, `arduino-uno-01`,
  `arduino-uno-light-01`, and `simulation-byte-buffer-validation`, all `Ready`;
- the Client lists `Arduino Uno Light Endpoint` and renders both instruments
  from the descriptor alone, with 20 read Properties, one writable Property,
  two Commands, and no endpoint-specific Client code;
- `Uv/SensorReady` and `Spectral/SensorReady` both report `True`, so both
  sensors initialized on the I2C bus;
- every Property reports `Quality: Good` with its declared unit, for example
  `Uv/A` 56 µW/cm², `Uv/C` 1 µW/cm², `Spectral/NIR` 1,636 counts,
  `Spectral/VisibleBottomRight` 17,999 counts;
- a Client write of `Uv/AlarmThreshold` was confirmed by the endpoint on
  readback, first `1`, then `0` to restore the device after validation;
- both `Uv/Measure` and `Spectral/Measure` executed successfully; and
- crossing the threshold produced exactly one `Uv/AlarmRaised` occurrence in
  the endpoint Event feed, attributed to `arduino-uno-light-01` and instrument
  `arduino-uno-light-uv-01`, with no payload.

### Increment 63D — Documentation closure

Documentation-only closure updates this ADR, `README.md`, `CLAUDE.md`,
`docs/ProjectStatus.md`, and `docs/Roadmap.md` to a consistent closed state
and records the physical validation result.

## Deferred scope

- The AS7331 device temperature. Reporting it needs a signed encoding, which is
  a separate wire-contract decision.
- Calibrated or gain-corrected spectral values. The endpoint reports raw
  acquisition counts; conversion belongs to an application above HASE.
- Gain, integration time, and LED control as writable Properties. Compact
  Version 1 has no enumeration encoding, and the sensors are used at their
  library defaults.
- Flicker detection from the AS7343.
- Same-VID/PID multi-board selection in `AttachCompactEndpointAsync`.
