# ADR-0040 Increment 40F3 — Desktop Diagnostics Panel

## Scope

Present the process-local diagnostic session in the Desktop Runtime Host WPF
application without adding level controls, clearing controls, persistence,
export, or remote access.

## Collection projection

`RuntimeDiagnosticsViewModel` consumes
`IDesktopRuntimeDiagnosticSource` on the existing one-second UI refresh cycle.
It exposes a read-only observable collection of 40F2 presentation entries,
ordered newest first.

Refresh projects only records not already represented by process-local
sequence. It removes records no longer retained by the bounded source, does not
duplicate entries on repeated refresh, and preserves selection while the
selected record remains retained.

An empty source clears the presentation. Non-overlapping records or a reused
sequence whose identity differs indicate a cleared or replacement diagnostic
session and rebuild the collection from the authoritative snapshot.

## WPF presentation

The Main Window adds a scroll-safe `Runtime Diagnostics` section with:

- retained-record count;
- an empty-state message;
- a master grid showing UTC timestamp, level, category, event, and endpoint;
- selected-record sequence, severity, identities, direction, duration, and
  outcome;
- ordered detail key/value rows; and
- byte count, truncation summary, and wrapping uppercase hexadecimal text when
  a bounded byte snapshot exists.

The list and selected-record content have their own scroll boundaries. Long
detail values and byte strings wrap and do not enlarge the application window
without bound.

## Composition

The application registers `RuntimeDiagnosticsViewModel`. `MainWindowViewModel`
receives and exposes it, refreshes it after runtime startup, and refreshes it
with the existing inventory timer. Snapshot projection remains local and does
not publish additional diagnostics.

## Verification

Focused tests cover initial newest-first projection, incremental and idempotent
refresh, selection preservation, empty-source clearing, clear followed by new
records, replacement-session sequence restart, collector eviction, and Main
Window composition.

Level selection and the operator Clear action remain deferred to Increment
40F4.
