# ADR-0041 Increment 41A — Diagnostics Window Lifecycle Foundation

## Scope

Introduce the modeless Desktop Runtime Host diagnostics-window lifecycle without
moving the existing ADR-0040 diagnostics presentation.

## Decision

The main host window exposes an `Open Diagnostics` command. The production
window service owns at most one diagnostics window:

- the first invocation creates and shows the window;
- later invocations restore and activate the existing window;
- closing the diagnostics window releases that instance;
- a later invocation creates a fresh window;
- the diagnostics window is owned by the main host window, so host shutdown
  closes it without allowing it to keep the process alive.

Closing the diagnostics window does not stop the runtime host, change capture,
clear retained records, or dispose the shared diagnostics view model.

## Presentation boundary

Increment 41A deliberately leaves the complete diagnostics presentation in the
main window. The new window contains only a lifecycle-validation shell and
shows the immutable capture level. Increment 41B moves the existing
presentation without changing capture semantics.

## Verification

Automated validation covers the main-window command boundary. Physical
validation confirms modeless positioning, single-instance activation,
independent closing, reopening, and orderly host shutdown.

No presentation pause, capture-level mutation, persistence, export, remote
retrieval, or northbound diagnostics contract is introduced.
