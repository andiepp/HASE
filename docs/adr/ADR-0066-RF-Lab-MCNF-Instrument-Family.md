# ADR-0066 — RF-Lab MCNF Instrument Family

- Status: Active; Increments 66A–66D complete, Increment 66E open
- Date: 2026-08-31
- Starting baseline: `2994ab8b7a226040cb8b662e4aaa887780996a3a`
- Starting complete Release baseline: 6,574 passed, 0 failed, 0 skipped

## Context

The RF-MiniLab is a homegrown RF signal laboratory: an Arduino Mega 2560
carrying a GRA & AFCH AD9910 DDS shield, an AD8307 50-Ohm detector, and an
Si5351 triple clock generator, attached to AEPRAKETE through a CH340
USB-serial adapter on COM12. It generates carriers from 100 kHz to
300 MHz with 0 to 80 dB of attenuation, amplitude and frequency
modulation, and AD9910 digital-ramp sweeps, and it reads the detector
through a 10-bit converter against the 2.56 V reference.

The device speaks MCNF, a framed binary command-response protocol shared
by several of the operator's applications, previously driven by a
dedicated .NET Framework WPF application. The objective is to publish the
instrument as an ordinary runtime-hosted endpoint so the existing
descriptor-driven Client presents it in both the secured private-network
profile and the development loopback profile, with no Client change.

Constraints established before implementation:

- The full protocol source exists beside the firmware; every frame layout,
  checksum, and unit conversion was characterized from source and then
  verified read-only against the physical node.
- The operator has further MCNF applications, so the protocol layer must
  not be RF-Lab-specific.
- The node offers no state readback for its signal path: DDS and clock
  settings can only be set. Every framed exchange, however, returns an
  acknowledged response after the function executed.
- The characterized firmware computes the response checksum only for
  successful responses; an error response carries an unspecified byte in
  the checksum position.
- The firmware's message-generator functions (0x30–0x32) store parameters
  but never transmit; their transmit modules are absent from the source.
- The runtime attachment host offers exactly one additional-service slot
  beyond the native-network, compact-serial, and in-process routes, and
  KEL-103 occupies it.

## Decision

### A generic MCNF layer, an RF-Lab family on top

`Hase.Mcnf` implements the protocol without any RF-Lab knowledge:
frame construction and parsing with the complement-of-sum checksum, the
sync-nibble channel rules, the standard node-administration and
read-configuration requests, and `McnfSession` — one serialized
command-response gate per node with timeout racing, the single-byte
connectivity test, explicit uncertain-outcome semantics after
transmission began, and a diagnostics observation contract mirroring the
SCPI layer. `Hase.Mcnf.Serial` bridges to `ISerialByteStream` and owns
the settle delay for nodes that reset when the port opens. A future MCNF
application adds its own family without touching this layer.

The RF-Lab family follows the KEL-103 five-layer shape:
`Hase.Mcnf.RfLab` holds the characterized codecs, ranges, sensor
conversions, node identity, and two immutable definitions;
`Hase.Mcnf.RfLab.Runtime` the session and runtime endpoint adapters; and
`Hase.Mcnf.RfLab.Hosting` the operational connection, publication,
recovery supervision, a five-second passive connectivity probe, the
operation mapping, and sanitized MCNF diagnostics.

### Set-only device state as staged targets and apply Commands

A HASE Command carries at most one argument, and the node cannot be asked
for its signal state. The definitions therefore follow the ladder rule:
version 1 is read-only — identity, detector level and voltage, indicator
state, clock-generator presence — and version 2 adds eleven writable,
host-staged target Properties together with eleven parameterless apply
Commands that push them to the node, mirroring how the instrument's own
front panel works. A mutation is transmitted once; the acknowledged MCNF
response is its execution confirmation; a missing response surfaces as an
explicitly uncertain outcome and is never replayed. Staged targets are
host state and revert to defaults when the host restarts, exactly as the
reference application behaves.

### Characterized wire discipline

The response checksum is verified only when the error byte reports
success, because the firmware demonstrably sends error responses without
one. A node-rejected request is a completed, healthy exchange: it surfaces
as a typed device error and is mapped to `Rejected` without faulting the
session. Identity is authoritative: publication requires the
node-type-information bytes `AE 70 10 80` before the first device
exchange. The message-generator functions are excluded from the model as
dead firmware code.

### Physical port behavior

The node communicates only with asserted DTR and RTS lines, so
`SerialTransportOptions` gained additive, default-off control-line
assertions applied at open. Opening the port resets the node; the
characterized settle is three seconds — a 1.5-second single shot loses
the first exchange while the node still boots, a failure the reference
application masked behind its five-second connectivity poll.

### Host integration behind the single additional slot

The composition file gains the strict `RfLabSerial` kind with the same
field shape as `Kel103Serial`. Preflight resolves and
`ReferenceEquals`-verifies the exact definition references before any
port is opened. `DesktopRuntimeHostInstrumentAttachmentRouter` shares the
attachment host's one additional-service slot by dispatching on the
connection-definition type between the KEL-103 and RF-Lab services; a
family's service provider is invoked only when at least one endpoint of
that family is configured. The transport API is not widened.

### What does not change

- The Client, the northbound contract, and the authorization model are
  unchanged. One composition entry publishes the instrument in both
  profiles; the generic descriptor-driven presentation renders it.
- The KEL-103 family, the compact and native routes, and the existing
  composition entries behave exactly as before; editor operations now
  thread RF-Lab endpoints through unchanged.

## Consequences

### Positive

- The instrument becomes a supervised, diagnosable runtime endpoint in
  both operating modes with zero Client code, confirming the
  descriptor-driven boundary for a second physical instrument family.
- The MCNF protocol layer is reusable for the operator's other MCNF
  applications.
- The additional-service slot now scales to further instrument families
  through composition instead of transport changes.

### Negative

- Staged targets are not device state: after a host restart the displayed
  targets are defaults until re-applied, and a value applied by another
  path is invisible to the host.
- The three-second settle delays every attach and recovery by that time.

### Neutral

- The firmware's checksum-less error responses weaken error-path framing
  verification; success paths remain fully verified.

## Increment plan

### Increment 66A — Generic MCNF stack and RF-Lab instrument family

Repository application: `Hase.Mcnf`, `Hase.Mcnf.Serial`,
`Hase.Mcnf.RfLab`, `Hase.Mcnf.RfLab.Runtime`, `Hase.Mcnf.RfLab.Hosting`,
five mirrored test projects, and the additive DTR/RTS extension of
`SerialTransportOptions`. Physical effects: none.

Result: complete as `7c17c81`; 6,772 passed, 0 failed, 0 skipped across
33 test projects; the 66-warning cold-build baseline unchanged.

### Increment 66B — Host integration

The `RfLabSerial` composition kind, the RF-Lab preflight and attachment
service, and the instrument attachment router, with focused tests beside
each seam. Physical effects: none.

Result: complete as `221355b`; 6,828 passed, 0 failed, 0 skipped across
33 test projects.

### Increment 66C — Read-only physical characterization

A bounded, read-only run on AEPRAKETE against the node on COM12 using the
new stack itself: connectivity test, node-type information, buffer-size
report, read-configuration, indicator state, and three sensor reads. No
mutating function; the error-status function was skipped because it
resets the node's error latch.

Result: complete. Every request frame matched the characterized bytes and
every success response verified: connectivity `A1` → `21`; node type
`AE.70.10.80`; reported buffer size 64 beside the real 128; configuration
with no variable sets, indicator on, Si5351 present; idle detector
readings at the floor. The first exchange after a 1.5-second settle was
lost while the node booted; three seconds succeeded immediately, and the
default settle was corrected accordingly as `c5ff089` with the complete
suite unchanged at 6,828.

### Increment 66D — Documentation

This ADR, `docs/ProjectStatus.md`, and `docs/Roadmap.md` record the
active objective consistently. Physical effects: none.

### Increment 66E — Deployment and physical validation (open)

Deployment of the `RfLabSerial` composition entry for COM12 on AEPRAKETE
in both profiles, Client refresh, LABC and LTAEP synchronization, and
supervised physical validation of the published endpoint, each as a
separately approved step. The objective closes only on its recorded
result.

## Deferred scope

- An MCNF byte interpreter for the host Diagnostics window.
- Descriptor-declared presentation (ADR-0065) for detector sweeps, and a
  host-side frequency-sweep measurement in the ANALYZE style of the
  reference application.
- The message-generator functions, pending firmware that transmits.
- The MCNF TCP transport that exists in the reference implementation.
- Named setting presets comparable to the reference application's files.
