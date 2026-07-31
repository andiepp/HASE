# ADR-0041 Increment 41C — Presentation Pause State

## Scope

Add presentation-only pause and resume semantics to
`RuntimeDiagnosticsViewModel` without changing the WPF diagnostics window.

## Pause

The initial state is running. Pausing makes periodic `Refresh()` calls inert.
The currently projected retained records, active display filter, displayed
records, and selection remain stable.

The diagnostic source is not paused. Publication, bounded source retention,
sequence assignment, and eviction continue normally.

Display-filter changes remain available and operate on the frozen projected
records. Clear remains an explicit operator action and clears both the source
and the frozen local projection while leaving presentation paused.

## Resume

Resume first changes the state to running and then immediately reconciles the
current bounded source snapshot. Records created and still retained during the
pause appear newest first. Records evicted during the pause do not reappear.
Selection follows the existing reconciliation rules.

Pause and Resume command availability is mutually exclusive.

## Deferred presentation

Increment 41C introduces no Pause button, Resume button, paused banner, or
accessibility text. Those presentation elements belong to Increment 41D.

## Verification

Focused tests cover initial state, command state, frozen entries and selection,
continued capture, bounded eviction, immediate resume reconciliation, filter
changes while paused, and explicit clearing while paused.
