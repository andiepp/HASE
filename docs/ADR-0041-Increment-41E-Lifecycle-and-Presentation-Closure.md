# ADR-0041 Increment 41E — Lifecycle and Presentation Closure

## Scope

Finalize the relationship between the modeless diagnostics-window lifecycle and
the process-local presentation state.

## Session ownership

`RuntimeDiagnosticsViewModel` is a singleton for one Desktop Runtime Host
application session. A dedicated `IDesktopDiagnosticsWindowFactory` creates
each WPF window and receives that same singleton instance.

Closing the diagnostics window therefore does not reset:

- paused or running state;
- frozen retained projection;
- display filter;
- selected record; or
- current presentation counts.

Reopening creates a fresh window over the same presentation session.

## Capture while closed

Diagnostic publication and bounded source retention are independent of window
lifecycle. If the window is closed while presentation is paused, capture
continues. Reopening remains paused and frozen. Resume immediately reconciles
the current bounded source snapshot.

## Application lifecycle

The WPF diagnostics window remains owned by the main host window. Main-window
shutdown closes it and cannot leave the process alive.

A new application process creates a fresh singleton view model in Running
state. No paused state, filter, selection, or retained presentation is persisted
across application restarts.

## Verification

Focused tests cover shared ownership, running and paused reopen behavior,
projection/filter/selection preservation, capture while closed, resume
reconciliation, and repeated open/close cycles.
