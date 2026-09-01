# ADR-0067 — Client-Hosted Instrument Panels

- Status: Closed; Increment 67O the stored-settings column
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

### The panel never disables itself to reach the device

An apply is one round trip to the Runtime Host. Disabling the panel for
its duration, which is what the first implementation did, made the whole
surface grey and repaint on every dial movement — a flicker the original
never had, because it owned the serial port and applied synchronously.

A panel-wide disable cannot be moved to the long-running modes either.
The root grid carries the Start control, so disabling the panel during a
sweep or an analysis would leave the operator no way to stop the run. The
controls that must not be touched mid-run are gated individually, as the
original gated them.

What the disable did protect, incidentally, was overlap: a dial reports
every intermediate value it passes through, so one movement raises
several applies, and the last to finish need not carry the newest value.
That is now handled where it belongs — one apply in flight and one
pending, the pending one re-reading the current values when it runs. The
panel issues one apply per round trip rather than one per reported value,
and the generator ends at the value the operator stopped on.

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
- Carrying the controls over unchanged carried their constraints over
  too. One of them — a control that owns its own data context — silently
  defeats any binding written against it, which is how the clock outputs
  came to display a state they do not act on.

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

### Increment 67G — The panel stops disabling itself

The panel-wide disable around every apply is removed and replaced by the
coalescing guard described above.

Result: complete as `3e424b7`, jointly with 67H. Validated on the
instrument: the operator confirmed the flicker is gone.

### Increment 67H — The clock outputs return

The Special Signals tab is restored carrying the Si5351 clock outputs.
The tab had been left out because the message generator does not
transmit, but the clock outputs share it and do transmit, so working
function was lost with the dead function. The message generator stays
out.

Each channel writes its own target and executes its own apply command, so
a clock change touches neither the other channels nor the signal path,
and each channel coalesces as the signal path does. No definition change
and no redeployment were needed: the Properties and Commands have been in
the descriptor since ADR-0066, and only the surface was missing.

Result: complete as `3e424b7`, jointly with 67G; 6,899 passed, 0 failed,
0 skipped across 34 test projects; the 66-warning cold-build baseline
preserved. The two increments share the view model and the test double
and were validated as one tree, so they are recorded in one commit rather
than a split whose intermediate state was never validated.

The tab and its controls were confirmed on the instrument. Their
enablement was not: the controls stay inert because of the binding defect
recorded under known defects.

### Increment 67I — Documentation closure

This increment records 67G and 67H, the design position that a panel
never disables itself, and the two client defects found while validating
them. Its account of the first defect was wrong and is corrected above.

### Increment 67J — The clock outputs reach the panel

Each clock control names its binding source, so its enablement follows
the panel rather than resolving against the control itself. Tests run the
interface on a dedicated thread and assert all three states: that the
control owns its data context, that a plain binding does not reach the
panel, and that an explicitly sourced one does.

Result: complete as `0ecc4e7`; 6,902 passed, 0 failed, 0 skipped across
34 test projects.

Physically validated: with the client republished, the operator confirmed
the clock controls respond and that a frequency commanded from the panel
changes the output, measured on an oscilloscope. Increment 67N records
the independent confirmation and the route this took through the record.

The panel's clock defaults do not match what the node boots to — the
panel starts channel two at 3 MHz where the firmware sets 25 MHz. That is
the staged-target model of ADR-0066 showing through rather than a fault:
the node offers no readback, so the panel shows what has been staged, not
what the hardware is doing. It also means the panel cannot reveal the
defect above, because it displays the target either way.

### Increment 67K — The endpoint pane can submit a write

The write command's predicate keeps only what changes when the workspace
changes, matching command execution, so the button's own enablement
binding is free to follow the typed value.

Result: complete; 6,907 passed, 0 failed, 0 skipped across 34 test
projects. Tests assert that the command's answer does not move with the
typed value, that the enablement binding does and announces each change,
and that the coarse gates still refuse a read-only Property and a
disconnected workspace.

### Increment 67L — Documentation closure

This increment records 67J and 67K, corrects the account 67I gave of the
binding defect, and marks both defects resolved. It also recorded the
clock outputs as physically validated, which was false; 67M withdraws it.

### Increment 67M — Documentation correction

This increment withdrew the physical validation 67L claimed for the clock
outputs, on the grounds that a commanded frequency was acknowledged while
the output stayed where start-up left it. That measurement has not
reproduced and 67N withdraws the withdrawal.

An objective whose record says a thing was validated when it was not is
worse than one that says nothing, because the next person has no reason
to look. That reasoning stands; this increment simply applied it to a
reading that was itself wrong.

### Increment 67N — The clock outputs, measured

The clock outputs are physically validated. Two independent routes were
measured on an oscilloscope: a frequency commanded from the panel, and a
frequency commanded through the northbound interface from outside the
Client with no user interface in the path, staged and applied as the
endpoint pane does it. Both moved the output to the commanded value.

The record took a poor route to a correct conclusion, which is worth
stating plainly for anyone reading it later. Increment 67L asserted this
validation before it had been measured. Increment 67M withdrew it on a
single contrary reading. Only here was it measured deliberately, with a
value chosen so that it could not be confused with a start-up default or
with any earlier attempt.

One observation is left unexplained: a frequency commanded through the
same northbound route was once acknowledged while the output read as its
start-up value. It has not recurred across later attempts by either
route. It is recorded as unexplained rather than as a defect, because a
defect that cannot be reproduced and contradicts every subsequent
measurement is a claim, not a finding.

The radio-frequency output recorded under increment 67F is a separate
matter and remains open: the detector does not respond to commanded
attenuation, and nothing here bears on it.

### Increment 67O — The stored-settings column

The preset list the original panel offered, deferred since 67B for want of
a store and a location. It now has both.

Presets are read from a directory the composing application names,
defaulting to the client's own configuration folder, which an application
update preserves because it replaces only the program. The panel carries
no machine knowledge, because a path that exists on one computer need not
exist on another. A directory that is missing or unreadable yields no
presets rather than preventing the panel from opening, and a name that
would resolve outside it is refused.

The parser reads the operator's own files as the original wrote them and
is forgiving of what they contain: a malformed line is skipped rather
than costing the whole preset, a value may contain commas, and a value
the file omits leaves the panel alone rather than commanding zero.
Settings for surfaces this panel does not present are read and ignored
rather than dropped.

Selecting a preset loads and applies it, as the original did, and applies
are suppressed while it loads. A preset sets many targets and each one is
live, so applying as they are set would command the instrument through
the preset's intermediate states: a new frequency against an old
amplitude, which is a real state on the output and not merely an extra
round trip. Everything is staged, including the mode, and then applied
once, with each clock channel the preset carried. The original did the
same with its own apply.

Reading only. The original also saved, renamed and deleted presets and
offered a directory picker. Those are absent by decision rather than
oversight; they raise questions about naming and overwriting that deserve
their own increment.

Result: complete as `146104d`; 6,940 passed, 0 failed, 0 skipped across
34 test projects; the 66-warning cold-build baseline restored, having
drifted to 68 since increment 67K. Physically validated: with the client
republished and the operator's own files in place, the column lists them
and applying one works.

Its commit names it increment 67R. The letters O, P and Q were used in
conversation for repository tooling that is no part of this objective,
and the commit took the next letter after them. It is recorded here as
67O, this ADR's next increment; the discrepancy is stated rather than
corrected, because the commit is pushed.

## Defects found while validating

Both were found while validating 67G and 67H on the instrument, recorded
before they were understood, and fixed in 67J and 67K.

- **A ported control cannot be bound to the panel.** `NCMultiDigit` sets
  `DataContext = this` in its constructor, so a binding written on that
  element resolves against the control rather than the panel. The clock
  outputs therefore never tracked `IsClockGeneratorPresent`. The binding
  was carried over verbatim from the original, which has the same flaw
  and never showed it. Fixed in 67J by naming the binding source; the
  deferred conversion of these controls to dependency properties would
  remove the class of fault.

  Increment 67I described the effect of this wrongly, saying the controls
  stayed disabled. A binding that cannot resolve leaves the property at
  its default, and the default here is enabled, so the controls were
  always enabled and simply ignored the clock generator. The tests
  written for 67J assert this directly.

- **The generic Selected Endpoint pane could not submit a write.** Its
  Write button carries both a `Command` and an enablement binding, and
  the host combines them so that the command decides. Whether the typed
  value is valid was inside the command's predicate, and nothing
  re-queries a command while the operator types, so the button's state
  was frozen from the last projection and a valid value could not enable
  it.

  Command execution never had this fault: its predicate carries only what
  changes when the workspace does, and the per-keystroke validity lives
  in the button's own binding, which follows the item directly. Fixed in
  67K by giving the write path the same shape. The write already
  re-checked the value before submitting it, exactly as command execution
  does, so nothing was made less safe.

  This belongs to the Client rather than to this objective — it affected
  every writable Property on every endpoint — and is recorded here only
  because this objective's validation found and fixed it.

- **Withdrawn: a commanded clock frequency does not reach the hardware.**
  Increment 67M recorded this on a single measurement. It has not
  reproduced, and 67N withdraws it: the clock outputs are physically
  validated. See that increment for what was measured and for the one
  observation that remains unexplained.

## Deferred scope

- Saving, renaming and deleting stored settings, and choosing their
  directory from the panel. Reading them is increment 67O; writing them
  raises questions about naming, overwriting and custody that deserve an
  increment of their own.
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
