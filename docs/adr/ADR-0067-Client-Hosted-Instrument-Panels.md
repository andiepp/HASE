# ADR-0067 — Client-Hosted Instrument Panels

- Status: Closed; Increment 67F documentation closure
- Date: 2026-08-31
- Starting baseline: `62d5e880dfc17fbc034cfa05e3d3cb3b0bc1fb96`
- Starting complete Release baseline: 6,828 passed, 0 failed, 0 skipped

## Context

ADR-0066 published the RF-MiniLab as a runtime-hosted instrument, and the
Client rendered it the way it renders every instrument: a list of
Properties with `Read` buttons and a list of Commands. That was a
faithful reading of the descriptor and a misreading of the request. The
operator had asked for the interface they had designed and used for years
— an existing WPF application with a nixie-style frequency display,
amplitude and frequency dials, and a measurement chart — to be the module
inside the Client.

Two established positions had to be reconciled.

ADR-0065 states that the Client knows no device, and explicitly rejected
special-casing an endpoint in the Client as the option that "puts device
knowledge into a component whose stated principle is that it has none".
A button that appears for one known endpoint identity is exactly that.

The interface itself is a substantial artifact and not neutral code:
roughly 1,900 lines of XAML and 1,100 lines of control code targeting
.NET Framework 4.7.2, whose view model *is* the MCNF device controller
and drives the serial port directly. It charts with a licensed
third-party component whose licence key is embedded in its source, and
one of its tabs targets firmware functions that never transmit.

The question this ADR answers is therefore not "how do we show this
instrument", but: how can a presentation layer that knows no device host
a surface designed for one particular device?

## Decision

### A descriptor declares a panel; the client resolves it

`InstrumentDescriptor` gains an optional `InstrumentPresentation`
carrying a `PanelId`, exposed northbound as additive field 13. The
identifier is a bounded token of ASCII letters, digits, hyphens, and full
stops: a descriptor may name a surface, and may neither carry arbitrary
text into a presentation layer nor grow without bound.

This carries capability, not appearance. It states that a dedicated
surface exists for this instrument and what it is called; it prescribes
no window, layout, or control. The mechanism is the same shape as
ADR-0065's property presentation, and generalizes the same way: any
future instrument — including the operator's other MCNF applications —
declares its own panel identifier and is offered its own surface.

### The library hosts no panel; the application composes them

`Hase.Client.Wpf` owns a registry and knows how to offer a panel, but
ships none. `Hase.Client.Wpf.App` composes the panels it ships into that
registry. An endpoint is offered a panel only when its declaration and a
hosted panel agree and the endpoint is `Ready`; a declaration this client
does not host presents exactly as no declaration. A client composed
without panels behaves as one that has no panel concept at all.

The button lives in the endpoint's list entry behind a data trigger, the
same conditional-rendering mechanism every other client view uses, and
the panel opens as a detached window following the established
Diagnostics and Media pattern (ADR-0057).

### A panel's only route to the device is bounded

`IRuntimeHostInstrumentOperations` binds Property reads and writes and
Command execution to one attachment and one instrument, routed through
the very session the workspace uses, so a panel behaves identically in
single-host and multi-host mode. A panel opens no transport, speaks no
device protocol, and cannot reach another attachment.

### The panel is the original interface

The styles, the six numeric and dial user controls, and the panel window
are the operator's own files, carried over with their visual identity
intact. Three deliberate surgeries were required and no more: the six
protocol-library converters gave way to one local status converter — only
one was ever used; the licensed chart became an in-repository polyline
control, so no licensed dependency and no licence key enter the
repository; and the message-generator tab stayed out while its firmware
does not transmit.

What could not be carried over is the view model, because it *was* the
device controller. Its replacement drives the instrument through the
bounded operations above while preserving the bound member names, so the
original view binds essentially unchanged.

### Live targets apply at once

Changing frequency, amplitude, modulation frequency, AM depth, or FM
deviation applies the signal immediately, the way the original panel's
dials drove the generator and the way a signal generator behaves. Sweep
and measurement values are staged and take effect when the sweep or the
measurement starts.

### ANALYZE sweeps from the panel

The original ANALYZE mode steps the carrier across the sweep span, lets
the detector settle, reads it, and plots the response over frequency.
Increment 67D deferred it, reasoning that a client-side loop is bounded
by round-trip latency and that a host-orchestrated sweep would belong to
the instrument family. That deferral is reversed here, and the reasoning
that produced it was only half right.

The latency is real and it does set the pace: three round trips per point
cost about 110 milliseconds, so a hundred-point analysis takes about
eleven seconds however short a duration is commanded. That is acceptable
for a bench measurement, and it is visible rather than hidden, because
the panel reports each point as it is taken.

The other half was wrong. A host-orchestrated sweep has nothing to
orchestrate: the node offers no sweep-and-measure function to delegate
to, so the family would have to acquire a measurement policy — how long
to settle, how many points, what to do when a reading fails — that its
device does not have. The panel is where the operator sets those values,
so the panel runs the loop and the instrument family stays set-only.

Two deviations from the original, which owned the serial port directly.
The point count follows the panel's measurement-count field rather than
the original's fixed 500, because each point costs three round trips
here. And the settling delay subtracts the time those round trips already
consumed, so a commanded duration stays the duration of the run instead
of becoming a lower bound on it.

### Versions stay immutable

The declaration is a new definition version. RF-Lab version 3 is version
2 plus the panel declaration, with an identical interface in every
Property and Command; versions 1 and 2 are untouched, and an operator
opts into the panel through configuration.

### Ported code keeps its own language level

The ported interface predates nullable reference types. Rather than
rewrite working instrument code to satisfy a compiler setting, the panel
project disables nullable and every file written for HASE opts in with
`#nullable enable`. This preserved the 66-warning cold-build baseline,
which the ported code had otherwise pushed to 126.

### What does not change

- The northbound API version, the authorization model, and the
  descriptor-driven rendering of every instrument that declares no panel.
- The KEL-103 family, the compact and native routes, and ADR-0066's
  instrument model, definitions 1 and 2, and host integration.

## Consequences

### Positive

- The instrument is operated through the interface designed for it,
  inside the Client, in both operating profiles, without the Client
  library learning what an RF-Lab is.
- The mechanism generalizes to further instruments and further panels
  through declaration and composition rather than client edits.
- A licensed charting dependency and an embedded licence key were kept
  out of the repository.

### Negative

- The client *application* now carries device-specific presentation. That
  is a deliberate move of the boundary: the library stays device-free,
  the application does not.
- A panel binds one definition's exact contract, so a panel and its
  definition version evolve together.
- The ported code keeps its original shape — no nullable annotations, and
  values pushed into controls through events rather than bindings —
  which is a maintenance seam for anyone editing it later.
- An analysis is paced by the transport rather than by the measurement.
  Three round trips per point put a hundred-point run at about eleven
  seconds whatever duration is commanded.

### Neutral

- The preset list of the original application is not part of this panel;
  see deferred scope.
- ANALYZE's mechanism is validated against the instrument; its
  measurement is not, for a reason outside HASE recorded under increment
  67F.

## Increment plan

### Increment 67A — Descriptor-declared panels and generic dispatch

The core declaration, the additive northbound field with its mappers and
pinned contract tests, the panel registry and bounded instrument
operations, the projection, and the list-entry button. No panel is
composed, so the workspace behaves exactly as before.

Result: complete as `542e8fd`; 6,859 passed, 0 failed, 0 skipped across
33 test projects.

### Increment 67B — The RF-Lab operating panel

The ported styles, controls, and window; the in-repository chart; the
view model over the bounded operations; RF-Lab definition version 3; and
the composition of the panel into the application.

Result: complete as `a9d1c70`; 6,875 passed, 0 failed, 0 skipped across
34 test projects; the 66-warning cold-build baseline preserved.

### Increment 67C — Deployment and physical validation

Result: complete. The Runtime Host and Client on AEPRAKETE were
republished from `a9d1c70` and all three endpoint compositions moved to
definition version 3 with timestamped backups. The declared panel
identifier `rf-lab-signal-lab` was verified arriving over the northbound
API and resolving against the hosted panel; the operator confirmed that
the button in the endpoint list entry opens the RF-MiniLab window. The
panel then drove the physical instrument through the live-target path a
dial movement triggers: 21.4 MHz and 40 dB attenuation, each
acknowledged, with the detector read back and no error reported.

### Increment 67D — Documentation closure

This ADR, `docs/ProjectStatus.md`, and `docs/Roadmap.md` record the
objective consistently. The ADR document itself was written in this
increment; increments 67A through 67C referenced it before it existed,
which is recorded here rather than quietly corrected.

### Increment 67E — The ANALYZE sweep

The panel steps the carrier across the sweep span, settles, reads the
detector, and plots the response, returning the generator to the panel's
own carrier when the run ends or is stopped. The ported window needed no
change: its ANALYZE selection was inert only because the replacement view
model had lost the original's mode gating, in which `IsModeSWEEP` covers
both the swept and the analysing mode.

Result: complete as `3da646b`; 6,884 passed, 0 failed, 0 skipped across
34 test projects; the 66-warning cold-build baseline preserved.

### Increment 67F — ANALYZE physical validation and documentation closure

The Client on AEPRAKETE was republished from `3da646b` and ANALYZE was
run twice against the physical instrument, from 10 to 30 MHz over 100
points at 20 dB attenuation with a commanded duration of 8 seconds. Both
runs stepped every point, took 10.9 seconds, reported no protocol error,
plotted all 100 readings, and returned the generator to the panel
carrier. The mechanism is validated.

The measurement is not. The detector reported a flat −72.8 to −71.9 dB
across the whole span, which is its no-signal floor. A follow-up probe
then held the carrier at 10 MHz and stepped the commanded attenuation
from 0 to 80 dB: the detector stayed between 375 and 380 millivolts
throughout, a spread of one converter count. An 80 dB change in commanded
output produced no change at the detector.

The evidence places that fault in the analogue path rather than assuming
it there. The detector is powered and working, because an unpowered board
reads near zero volts at the converter instead of the several hundred
millivolts an AD8307 sits at with no input. The command path matches the
firmware, which reads the frequency in hertz, negates the amplitude
magnitude exactly as the family transmits it, and drives the single-tone
profile. The output is enabled in every session, because initialization
drives the power-down pin low, the node resets when the port opens, and
this firmware build compiles the front-panel output button out. The
panel's own calibration anchors roughly 2,235 millivolts at about 0 dB,
so a detector on the output at full drive should read about 2.2 volts —
some 72 dB above what it reads.

The measurement path is therefore recorded as an open physical matter and
not as a HASE defect. This increment also records increment 67E, which
was added after the 67D closure; ADR-0065 set the precedent with its own
65D and 65E.

## Deferred scope

- The stored-settings list of the original application. Presets are
  client-side state and need a store and a location; the panel presents
  instrument identity in that column meanwhile.
- The message-generator surface, pending firmware that transmits.
- Clamping the panel's amplitude to the range the node can produce. The
  firmware accepts −72 to 0 dBm, so the panel's full 80 dB of attenuation
  asks for a level the DDS cannot reach, and the node neither rejects the
  request nor reports the clipping.
- The declared detector floor. The sensor minimum is −70 dB while the
  characterized transfer puts the no-signal floor near −72.3 dB, so a
  reading taken with no signal present sits below the chart axis.
- Converting the ported numeric controls to dependency properties, which
  would let the panel bind them instead of wiring their events.
