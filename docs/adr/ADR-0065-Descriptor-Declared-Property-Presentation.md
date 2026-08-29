# ADR-0065 — Descriptor-Declared Property Presentation

- Status: Closed; Increment 65D spectral curve completion
- Date: 2026-08-29
- Starting baseline: `5f07edd4194c6c618ec1bf0cc203ea1a95c63236`
- Starting subject: `ADR-0063: Arduino Uno Light endpoint with AS7331 and AS7343`
- Starting complete Release baseline: 6,552 passed, 0 failed, 0 skipped

## Context

The Client renders Properties one below another, each with its own value,
timestamp, quality, and `Read` button. That is correct and device-independent,
and it is the right default for an unknown instrument.

It reads poorly for an instrument whose Properties belong together. The
`arduino-uno-light-01` AS7331 instrument publishes three UV irradiances that are
one reading of one sensor, and its AS7343 instrument publishes fourteen channels
that together sample a spectrum. Rendered independently they occupy twenty
vertical blocks with twenty `Read` buttons and twenty timestamps, and the
spectral shape — the actual information — is invisible.

Nothing in the published model lets a presentation layer do better. The
descriptor states each Property's identity, path, type, unit, range, and access
mode, but never that two Properties belong to one reading, and never where a
channel sits on a shared axis. The centre wavelength of channel `F1` exists only
inside its display name, `F1 405 nm`.

Three ways to close that gap were considered:

1. **Special-case the endpoint in the Client.** Fastest, and it produces exactly
   the intended layout. It also puts device knowledge into a component whose
   stated principle is that it has none, and every further instrument needs
   another special case.
2. **Infer from what the Client already receives.** Group by shared instrument,
   quantity, and unit, and parse the wavelength out of the display name. No
   contract change, but the chart would then depend on presentation text.
   Renaming or translating a display name would silently change the physics.
3. **Declare the relationship in the descriptor.** A contract change, carried
   northbound like every other descriptor fact.

## Decision

The descriptor gains optional presentation metadata, and the Client renders from
it generically.

### Core model

`PropertyDescriptor` gains an optional `Presentation` member of the new type
`PropertyPresentation`, which declares:

- `GroupId` — Properties of one instrument sharing this identifier form one
  logical reading. Group identifiers are scoped to their instrument.
- `Abscissa` — the Property's coordinate on the independent axis shared by its
  group, as the new `QuantityValue` type: a scalar with its `Unit`.

`QuantityValue` is a concrete value with a unit, distinct from
`NumericDataDescriptor`, which describes the shape every value of a Property
takes. `Hase.Core` also gains the unit `nanometre` for the length quantity.

This carries relationship, not appearance. It states that Properties belong
together and where they sit on a shared axis. It does not prescribe a control, a
layout, or a colour; how to draw a group remains entirely the presentation
layer's decision. Every member is optional, and a Property without presentation
metadata renders exactly as before.

### Northbound contract

`PropertyDescriptor` gains field 7, `presentation`, carrying the new
`PropertyPresentation` and `QuantityValue` messages. The change is additive:
no existing field number, type, or meaning changes, so a client built against
the previous contract ignores field 7 and behaves exactly as before. The
reported API version is unchanged for that reason.

### Client presentation

`InstrumentInventoryItemViewModel` groups its Properties by declared group
identifier and exposes `PropertyGroups` beside `UngroupedProperties`, which is
the same shape it already uses to present related Commands. A group renders as:

- **a curve**, when at least two members declare both an abscissa and a numeric
  value: the members are ordered along the abscissa, the abscissa range is
  mapped across the plot width, and values are mapped against the group maximum
  with zero on the baseline; or
- **a compact row** otherwise: the member values side by side, one shared unit,
  one timestamp, and one `Read` button that reads every readable member.

The group timestamp is the timestamp of the least recently acquired member, so
a group can never read as fresher than its stalest value, and is reported as
unknown while any member has no acquisition timestamp at all.

The Client contains no endpoint, descriptor, or device identifier. Any
instrument declaring the same metadata renders the same way.

### Arduino Uno Light declaration

The AS7331 instrument declares group `uv-irradiance` on its three irradiance
Properties. The writable alarm threshold and the readiness Property stay
ungrouped, because they are not part of the reading.

The AS7343 instrument declares group `spectral-scan` with a nanometre abscissa
on every channel that has a centre wavelength: `F1` 405, `F2` 425, `FZ` 450,
`F3` 475, `F4` 515, `F5` 550, `FY` 555, `FXL` 600, `F6` 640, `F7` 690, `F8`
745, and `NIR` 855. The two broadband visible channels and the readiness
Property stay ungrouped.

### Selection survives inventory refresh

A defect, fixed with the same increment because it made the new presentation
unusable: the selected endpoint lost its visual indication within a second.

The endpoint projection is immutable and is replaced completely whenever the
observation state changes, which is continuous for an endpoint whose values
move. The list control clears its own visual selection when its item source is
replaced, and re-asserting the bound selection afterwards does not restore it,
because the control settles its selection after the notification. The logical
selection always survived — `SelectedEndpoint` resolves by attachment key — so
the detail pane stayed populated while the tile stopped looking selected.

The fix is the pattern the Runtime Host list in the same window already uses
successfully: the tile draws its selection from an `IsSelected` flag on the
projected item, re-applied to every rebuilt projection, instead of from the
control's own visual state. `EndpointInventoryItemViewModel` additionally
compares by attachment key alone, so a retained item still matches its
replacement; selection is presentation state and is excluded from that
equality.

This was always latent. It became constant with an endpoint whose values move.

## Consequences

### Positive

- Related Properties can be presented as one reading without any component
  knowing which device produced them.
- A sampled curve is described by the model rather than inferred from text, so
  renaming or translating a display name cannot change what is plotted.
- The metadata is reusable: any future spectrometer, sweep, or multi-channel
  sensor gets grouped and plotted presentation by declaring it.
- Twenty vertical blocks with twenty buttons become two readings.

### Negative

- A contract change, with mappers and pinned contract tests on both sides.
- The Client gains chart geometry, which is presentation logic it did not have
  before. It is computed in the view model and unit-tested rather than drawn in
  code-behind.

### Neutral

- Purely additive at every layer. Instruments that declare nothing are
  unaffected, and so is every existing test of them.
- Grouping is descriptive, not authoritative: a presentation layer may ignore
  it entirely and render each Property on its own.

## Increment plan

### Increment 65A — Repository application

Goal: the core model, the northbound contract, the Arduino Uno Light
declaration, the Client presentation, and the selection fix, with focused tests
at each layer.

Automated validation: focused `Hase.Core.Tests`,
`Hase.Runtime.Remote.Grpc.Contracts.Tests`,
`Hase.Runtime.Remote.Grpc.Adapter.Tests`, `Hase.Client.Grpc.Tests`,
`Hase.DesktopHost.Tests`, and `Hase.Client.Wpf.Tests`, then the complete
Release suite.

Physical or deployment effects: none.

Rollback boundary: the working tree before the increment.

Definition of done: the complete Release suite passes, and the contract test
pins the new field and messages rather than tolerating them.

Result: 6,572 passed, 0 failed, 0 skipped across 28 test projects, from the
6,552-test starting baseline.

The figure first recorded here was 6,570, measured before the last two
selection tests were added. Those were covered by the focused
`Hase.Client.Wpf.Tests` suite, and the complete suite was not re-run before
the commit. Corrected at Increment 65D, which re-measured the same code.
first complete run and passed both in isolation and on a clean rerun; it is the
known load-sensitive test and is unrelated to this change.

### Increment 65B — Physical validation

Goal: confirm the rendered result against the real endpoint on AEPRAKETE.

Physical or deployment effects: starts the development Runtime Host and the
Client. No firmware, deployment, or configuration change.

Definition of done: the endpoint selection stays visibly selected across
refreshes; the three UV irradiances render as one row with one `Read` button
and one timestamp; the nine declared spectral channels render as one curve
against wavelength; and the ungrouped Properties are unchanged.

Result: complete, against `arduino-uno-light-01` on the development profile.

- The endpoint tile stays visibly selected. It was still selected 25 seconds
  after selection, across continuous spectral refreshes, where it previously
  lost the indication in under a second.
- The AS7331 instrument renders one `Uv Irradiance` group: UV-A 3, UV-B 3, and
  UV-C 1 side by side, each with its quality, above one shared
  `Unit: µW/cm²`, one timestamp, and one `Read` button.
- The AS7343 instrument renders one `Spectral Scan` curve of the nine declared
  channels, plotted against wavelength from 405 nm to 855 nm with the group
  maximum stated as 160 counts, above one shared unit, one timestamp, and one
  `Read` button.
- The ungrouped Properties are unchanged: the writable alarm threshold with its
  editor, the readiness Property, and the Commands render exactly as before.

The first validation attempt rendered the previous layout because the desktop
shortcut launches the installed Client, which still carries the previously
published build. Validation used the repository Release build. Refreshing the
installed Client is a separate deployment decision.


### Increment 65C — Documentation closure

Documentation-only closure updates this ADR, `README.md`, `CLAUDE.md`,
`docs/ProjectStatus.md`, and `docs/Roadmap.md` to a consistent closed
state.

Result: complete. ADR-0065 is closed at 6,575 tests across 28 test
projects.

### Increment 65D — Spectral curve completion

Goal: take up the deferred `FZ`, `FY`, and `FXL` channels so the curve covers
every AS7343 channel with a centre wavelength.

The three channels were excluded at 65A only because the requested scope named
`F1` to `F8` and `NIR`. They are physically part of the same spectrum, and the
mechanism needed no change: declaring their wavelength is the whole edit.

Files modified:

- `src/Hase.DesktopHost.App/Physical/ArduinoUnoLightCompactDefinitionFactory.cs`
- `tests/Hase.DesktopHost.Tests/ArduinoUnoLightCompactDefinitionFactoryTests.cs`
- `docs/adr/ADR-0065-Descriptor-Declared-Property-Presentation.md`

Physical or deployment effects: none in the repository. The endpoint firmware,
the wire contract, and the Client are unchanged.

Definition of done: the curve carries twelve points in wavelength order, the

## Deferred scope

- Axis ticks, gridlines, log scaling, and zoom. The plot states range and
  maximum and nothing more.
- Grouping across instruments. Group identifiers are scoped to one instrument.
- A declared preferred order within a group that has no abscissa; members
  currently follow descriptor declaration order.
