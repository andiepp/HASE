# HASE SCPI Instrument Authoring Guide

This guide explains how a SCPI instrument is brought into HASE — the
layers to implement, the order to implement them in, and the discipline
each layer obeys. It is the SCPI counterpart of the
[ESP32 Endpoint Authoring Guide](ESP32-Endpoint-Authoring-Guide.md).

This repository ships the SCPI protocol and no SCPI instrument. An
instrument is authored in an add-on repository that consumes this one
(ADR-0068). The rules below are the ones the first such instrument, the
KORAD KEL-103, was built under and physically validated against; its
record is ADR-0044 through ADR-0049. Where a rule needs a concrete
instance, the KEL-103 is the case study, cited from that record rather
than as code to read alongside.

The audience is a C# developer at home in the repository. Adding an
instrument is authoring, not configuration.

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

| Layer | Where | What |
| --- | --- | --- |
| Transport-independent SCPI text session | `src/Hase.Scpi`, this repository | `ScpiTextSession`, `ScpiTextFramingOptions`, command and response terminators, request formatting, response framing, diagnostics observation |
| Serial byte stream | `src/Hase.Scpi.Serial`, this repository | `SerialScpiByteStream` and `SerialScpiByteStreamFactory` behind `IScpiByteStream` |
| Instrument model: definitions, mappings, characterized values | the add-on | one project per instrument family |
| Runtime adapter: normalized operations over the session | the add-on | typed observations and mutation results |
| Hosting: attachment, supervision, health, diagnostics | the add-on | the family's `IDesktopRuntimeHostEndpointProvider` and its collaborators |
| Application integration | the add-on's application | the `CreateEndpointProviders` override of the base `App` |

An instrument reuses the first two layers unchanged and adds its own
versions of the rest. Separate projects per layer are a convention, not a
requirement, but they keep the dependency direction visible: the model
knows nothing of the session, the adapter knows nothing of hosting.

## Step 1 — Characterize, read-only, before any other code

Nothing about an instrument's protocol is assumed; everything is
established by bounded, read-only experiments against the physical
device. Write the characterization as a small program in the add-on, a
console project or a test project with an explicit opt-in, composed from
`ScpiTextSession` over `SerialScpiByteStreamFactory`:

- Start with **identity**: one fixed `*IDN?` with an explicitly selected
  command terminator, bounded response collection, and recognition of the
  product identity — nothing else.
- Establish the serial framing empirically: baud, data bits, parity,
  command and response terminators, echo behavior. The KEL-103 turned out
  to be 115200 8N1, CR-terminated commands, LF-terminated responses, no
  echo; assume none of that for another instrument.
- Characterize every value the instrument will expose the same way:
  measurements, state, limits — each through fixed queries, each
  read-only.
- When a candidate query misbehaves, reject it after **one**
  transmission and record the rejection; the KEL-103's `:VOLTage? MIN`
  timed out once and was excluded, not retried.
- Mutating characterization (setter grammar, mode selection) comes last,
  under explicit operator approval, with restoration to the original
  state in the same run.

Keep the characterization report with the instrument, in the add-on; it
is the evidence every mapping string rests on.

One hard-won implementation lesson lives in this layer: on Windows,
`SerialPort.BaseStream.ReadAsync` may ignore cancellation while waiting
for a first byte. The session utilities race every read against an
independent timer and dispose the owned port when the timer wins — do not
rely on cancellation tokens as a serial timeout boundary.

`ScpiTextSession` gives you the serialized query/command pipeline,
framing, desynchronization faulting, and diagnostics observation for
free; your characterization and adapter code composes it with
`SerialScpiByteStreamFactory` rather than touching `System.IO.Ports`
directly.

## Step 2 — The versioned instrument definition

The definition declares what the instrument exposes as normalized
Properties and Commands — identifiers, paths, display names, data
descriptors, units — under one `DescriptorId` with an integer version.
The rules:

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
  form. Keep the exact characterized strings here, in one place, with
  their units and invariant formats.
- A small repository class serves the exact versioned definitions to the
  host.
- A Command that must not run by accident declares its presentation:
  explicit confirmation, membership of a mode-selection group, an input
  control. The Client renders what the definition declares (ADR-0068,
  68E); nothing in the Client names the instrument.

## Step 3 — The runtime adapter

The adapter turns session exchanges into normalized observations and
mutation results. Its shape:

- A **read-only session adapter** produces typed observations and
  complete synchronization snapshots — everything the host needs to
  publish and to resynchronize after recovery.
- The **endpoint adapter** adds mutations: transmit once, read back
  authoritatively, return a typed result. Enforce the instrument's
  interlocks here — the KEL-103 refuses mode and setpoint changes unless
  the input is authoritatively OFF, verified immediately before the
  single transmission.
- An interrupted mutation throws an uncertain-outcome exception instead
  of retrying; the supervision layer faults the session and the
  replacement session resynchronizes **read-only** — the mutation is
  never replayed.

## Step 4 — Hosting and supervision

The hosting layer owns the lifecycle around the adapter:

- An **operational connection** and its factory — one owned serial
  session per attachment, created fresh for every connection generation.
- A **published attachment** and its factory — identity verification and
  complete initial synchronization **before** publication; the endpoint
  never appears half-synchronized.
- A **supervisor** and a **connection slot** — replacement on fault,
  cache preservation while disconnected, read-only resynchronization on
  recovery.
- A **passive health monitor** — the ADR-0047 pattern: a fixed read-only
  identity probe, five seconds after the previous completed probe, only
  while `Ready`, through the same serialized gate as everything else; a
  failed probe faults the endpoint into ordinary recovery.
- **Property and Command operations** — the bridge to the normalized
  attachment operation ports.
- A **diagnostic observer** implementing `IScpiDiagnosticObserver` —
  sanitized Operational, Protocol, and Bytes diagnostics for every
  exchange, within the established levels.

## Step 5 — Application integration

The base application composes what it ships and nothing else; an add-on
application derives from it and contributes its families. There is no
runtime discovery, by decision (ADR-0068). The seams:

- Implement `IDesktopRuntimeHostEndpointProvider` from
  `src/Hase.DesktopHost/Hosting`: `ProviderId` names the family,
  `Supports` claims the composition entries that are yours,
  `CreateAttachmentService` composes the family's attachment service into
  the runtime, and `ResolveAttachmentsAsync` turns the composition into
  the attachments to supervise.
- In the add-on's application, derive from the base `App` and override
  `CreateEndpointProviders` to return a
  `DesktopRuntimeHostEndpointProviderRegistry` holding the base providers
  and yours. The base host publishes without your family and the add-on
  host publishes with it, from the same composition profile format.
- A Client panel for the family is optional and is the add-on's too
  (ADR-0067). Without one, the Client renders the instrument from its
  descriptor, which is the case every instrument must serve first.

Expect these changes to ripple into the add-on's focused test projects;
that is intended, not incidental. The base carries tests that fail if an
instrument name enters its application or its solution; an add-on that
needs something from the base asks for a seam, not for a name.

## Testing expectations

The repository's standard applies: every layer carries focused tests
beside it. For a new instrument, cover at minimum: framing and parsing
against characterized byte sequences, mapping correctness, interlock
enforcement, uncertain-outcome behavior, no-replay across simulated
recovery, and the health-probe lifecycle. The add-on's complete suite
must stay green against the base commit it pins; physical validation
against the real instrument is a separate, explicitly approved step.

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
- [ ] Endpoint provider implemented and composed into the add-on
      application, with its focused tests.
- [ ] No SCPI console, no secrets, no machine-specific values in any
      repository.

## Where this leads

Authoring a complete new physical instrument end to end — with real
characterization hardware — is recorded as deferred future work under
ADR-0061. Generic VISA, USBTMC, and GPIB transports and automatic
instrument discovery remain explicitly out of scope of the current
boundary.
