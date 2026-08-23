# HASE SCPI Instrument Authoring Guide

This guide explains how a new SCPI instrument is brought into HASE — the
layers to implement, the order to implement them in, and the discipline
each layer obeys. It is the SCPI counterpart of the
[ESP32 Endpoint Authoring Guide](ESP32-Endpoint-Authoring-Guide.md), and
it uses the complete KORAD KEL-103 implementation (ADR-0044 through
ADR-0049) as the worked reference: for every step, the repository contains
a finished, physically validated example to read alongside.

The audience is a C# developer at home in the repository. Adding an
instrument is authoring, not configuration — using an already-supported
instrument is [Example 5](examples/Example-5-KEL-103.md).

## The boundary

The accepted SCPI adapter boundary (ADR-0044) keeps everything
device-specific below HASE's normalized model:

- SCPI syntax, serial framing, query matching, response parsing, and
  device-specific errors never appear above the adapter. Clients see
  ordinary Properties and Commands.
- One serialized command/query pipeline per physical session. Queries,
  writes, health probes, and synchronization all pass through the same
  gate; nothing overlaps on the wire.
- **No mutating operation is ever retried or replayed.** A mutation is
  transmitted once, read back authoritatively, and an interrupted outcome
  is reported as explicitly uncertain rather than repeated.
- There is no arbitrary operator SCPI console, by decision.
- Publication happens only after authoritative identity verification and
  complete initial synchronization.

## The layers at a glance

| Layer | Project | KEL-103 reference |
| --- | --- | --- |
| Transport-independent SCPI text session | `src/Hase.Scpi` | `ScpiTextSession`, framing and terminator types |
| Serial byte stream | `src/Hase.Scpi.Serial` | `SerialScpiByteStream(Factory)` |
| Instrument model: definitions, mappings, characterized values | `src/Hase.Scpi.Kel103` | definition and mapping classes per version |
| Runtime adapter: normalized operations over the session | `src/Hase.Scpi.Kel103.Runtime` | `Kel103RuntimeEndpointAdapter`, observation and mutation-result types |
| Hosting: attachment, supervision, health, diagnostics | `src/Hase.Scpi.Kel103.Hosting` | `Kel103SupervisedAttachmentFactory` and its collaborators |
| Host application integration | `src/Hase.DesktopHost.App` | preflight, attachment composition, composition schema |
| Characterization utilities | `src/HASE.ProtocolExplorer/Scenarios` | the eight `Kel103*CharacterizationScenario` classes |

A new instrument reuses the first two layers unchanged and adds its own
versions of the rest.

## Step 1 — Characterize, read-only, before any other code

Nothing about an instrument's protocol is assumed; everything is
established by bounded, read-only experiments against the physical device.
The KEL-103 characterization scenarios in
`src/HASE.ProtocolExplorer/Scenarios` are the models to copy:

- Start with **identity**: one fixed `*IDN?` with an explicitly selected
  command terminator, bounded response collection, and recognition of the
  product identity — nothing else
  (`Kel103ReadOnlyCharacterizationScenario`).
- Establish the serial framing empirically: baud, data bits, parity,
  command and response terminators, echo behavior. The KEL-103 turned out
  to be 115200 8N1, CR-terminated commands, LF-terminated responses, no
  echo.
- Characterize every value the instrument will expose the same way:
  measurements, state, limits — each through fixed queries, each
  read-only (`Kel103ReadOnly*CharacterizationScenario`).
- When a candidate query misbehaves, reject it after **one**
  transmission and record the rejection; the KEL-103's `:VOLTage? MIN`
  timed out once and was excluded, not retried.
- Mutating characterization (setter grammar, mode selection) comes last,
  under explicit operator approval, with restoration to the original
  state in the same run (`Kel103SetpointWriteCharacterizationScenario`,
  `Kel103ModeSelectionCharacterizationScenario`).

One hard-won implementation lesson lives in this layer: on Windows,
`SerialPort.BaseStream.ReadAsync` may ignore cancellation while waiting
for a first byte. The session utilities race every read against an
independent timer and dispose the owned port when the timer wins — do not
rely on cancellation tokens as a serial timeout boundary.

`ScpiTextSession` in `src/Hase.Scpi` gives you the serialized
query/command pipeline, framing, desynchronization faulting, and
diagnostics observation for free; your characterization and adapter code
composes it with `SerialScpiByteStreamFactory` rather than touching
`System.IO.Ports` directly.

## Step 2 — The versioned instrument definition

The definition declares what the instrument exposes as normalized
Properties and Commands — identifiers, paths, display names, data
descriptors, units — under one `DescriptorId` with an integer version.
The rules, as practiced by `Kel103IdentityDefinition` through
`Kel103ControlledInputDefinition` in `src/Hase.Scpi.Kel103`:

- **Versions are immutable.** A published version is never edited; new
  capability is a new version.
- **Evolve additively and read-only-first.** The KEL-103 ladder is the
  case study: version 1 identity only; 2 adds read-only measurements;
  3 adds read-only state and targets; 4 makes targets writable and adds
  mode Commands; 5 adds input control and the separately confirmed SHORT
  activation. A user can always stop at a read-only version.
- **Safety-relevant capability gets its own version** so operators opt
  into it explicitly through configuration.
- Beside the definitions live the **mappings** — small classes tying each
  normalized Property or Command to its characterized query or setter
  form (`Kel103MeasurementMapping`, `Kel103SetpointMapping`,
  `Kel103ModeSelectionMapping`, …). Keep the exact characterized strings
  here, in one place, with their units and invariant formats.
- A small repository class
  (`Kel103DefinitionRepository`) serves the exact versioned definitions
  to the host.

## Step 3 — The runtime adapter

`src/Hase.Scpi.Kel103.Runtime` turns session exchanges into normalized
observations and mutation results. Its shape to mirror:

- A **read-only session adapter** (`Kel103ReadOnlySessionAdapter`)
  produces typed observations (`Kel103MeasurementObservation`,
  `Kel103OperatingModeObservation`, …) and complete synchronization
  snapshots — everything the host needs to publish and to resynchronize
  after recovery.
- The **endpoint adapter** (`Kel103RuntimeEndpointAdapter`) adds
  mutations: transmit once, read back authoritatively, return a typed
  result (`Kel103SetpointMutationResult`, …). Enforce the instrument's
  interlocks here — the KEL-103 refuses mode and setpoint changes unless
  the input is authoritatively OFF, verified immediately before the
  single transmission.
- An interrupted mutation throws
  `Kel103MutationOutcomeUncertainException`-style uncertainty instead of
  retrying; the supervision layer faults the session and the replacement
  session resynchronizes **read-only** — the mutation is never replayed.

## Step 4 — Hosting and supervision

`src/Hase.Scpi.Kel103.Hosting` owns the lifecycle around the adapter:

- `Kel103OperationalConnection(Factory)` — one owned serial session per
  attachment, created fresh for every connection generation.
- `Kel103PublishedAttachment(Factory)` — identity verification and
  complete initial synchronization **before** publication; the endpoint
  never appears half-synchronized.
- `Kel103PublishedAttachmentSupervisor` and
  `Kel103PublishedConnectionSlot` — replacement on fault, cache
  preservation while disconnected, read-only resynchronization on
  recovery.
- `Kel103PassiveHealthMonitor` — the ADR-0047 pattern: a fixed read-only
  identity probe, five seconds after the previous completed probe, only
  while `Ready`, through the same serialized gate as everything else; a
  failed probe faults the endpoint into ordinary recovery.
- `Kel103EndpointAttachmentPropertyOperations` /
  `...CommandOperations` — the bridge to the normalized attachment
  operation ports.
- `Kel103ScpiDiagnosticObserver` — sanitized Operational, Protocol, and
  Bytes diagnostics for every exchange, within the established levels.

## Step 5 — Host application integration

This is the part that is **KEL-103-specific today**, and a new instrument
extends it in place — there is no plugin mechanism, by current design.
The seams, all in `src/Hase.DesktopHost.App` and
`src/Hase.DesktopHost`:

- `Hosting/DesktopRuntimeHostKel103DefinitionPreflight.cs` accepts
  exactly the four attachable KEL-103 definition references. A new
  instrument adds its own preflight (or a generalized one) validating its
  definition references.
- `ProductionPrivateNetworkRuntimeHostBackend` composes the KEL-103
  attachment service into the runtime attachment host
  (`DesktopRuntimeHostKel103AttachmentService` in
  `Hosting/DesktopRuntimeHostKel103AttachmentInventoryAdapter.cs` and
  `DesktopRuntimeHostKel103AttachmentFactory` in
  `Hosting/DesktopRuntimeHostKel103AttachmentSet.cs`). A new instrument
  family adds its own attachment-service composition alongside.
- `Hase.DesktopHost/Configuration/DesktopRuntimeHostEndpointCompositionProfileFile.cs`
  parses the composition `kind` values (`Kel103Serial` today). A new
  family adds its `kind` with its required fields, keeping the strict
  unknown-field rejection.

Expect these changes to ripple into the corresponding focused test
projects; that is intended, not incidental.

## Testing expectations

The repository's standard applies: every layer carries focused tests
beside it — `Hase.Scpi.Tests`, `Hase.Scpi.Kel103.Tests`,
`Hase.Scpi.Kel103.Runtime.Tests`, `Hase.Scpi.Kel103.Hosting.Tests` mirror
the KEL-103 layers with hundreds of tests. For a new instrument, cover at
minimum: framing and parsing against characterized byte sequences,
mapping correctness, interlock enforcement, uncertain-outcome behavior,
no-replay across simulated recovery, and the health-probe lifecycle. The
complete suite must stay green; physical validation against the real
instrument is a separate, explicitly approved step.

## Authoring checklist

- [ ] Identity characterized read-only with bounded responses and
      explicit terminators; serial parameters established empirically.
- [ ] Every exposed value characterized through fixed queries; rejected
      candidates documented, never retried.
- [ ] Mutating characterization done last, operator-approved, with
      restoration in the same run.
- [ ] Definitions immutable and versioned, read-only versions first,
      safety-relevant capability in its own version.
- [ ] Exact characterized query strings kept in mapping classes only.
- [ ] One serialized session; mutations transmitted once with
      authoritative readback; uncertainty explicit; no retry, no replay.
- [ ] Publication only after identity verification and complete initial
      synchronization; recovery resynchronizes read-only.
- [ ] Passive health probing through the same serialized gate.
- [ ] Diagnostics sanitized at every level.
- [ ] Host preflight, attachment composition, and composition `kind`
      extended, with their focused tests.
- [ ] No SCPI console, no secrets, no machine-specific values in the
      repository.

## Where this leads

Authoring a complete new physical instrument end to end — with real
characterization hardware — is recorded as deferred future work under
ADR-0061. Generic VISA, USBTMC, and GPIB transports and automatic
instrument discovery remain explicitly out of scope of the current
boundary.
