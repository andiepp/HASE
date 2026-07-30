# ADR-0040 Increment 40F4 — Local Session Controls and Presentation Closure

## Scope

Complete Desktop Runtime Host diagnostic presentation with an explicit capture
level, cumulative display filtering, a local clear action, and Bytes-capture
warning.

## Capture level

`IDesktopRuntimeDiagnosticSource.MaximumLevel` exposes the immutable maximum
level selected when the local diagnostic session starts. The production backend
reports the configured startup level before, during, and after the session.

Capture remains startup-owned. The WPF display cannot install Protocol or byte
observers after endpoint connections are created.

## Display filter

`RuntimeDiagnosticsViewModel` exposes only cumulative display levels at or below
the session capture level. The initial filter equals that capture level.

Filtering changes the visible presentation only. Filtered-out entries remain in
the bounded session and reappear when the display level is raised. If filtering
removes the selected entry, the newest visible entry becomes selected.

## Local clearing

`ClearDiagnosticsCommand` clears the current process-local collector and
immediately reconciles the presentation. It is disabled while no records are
retained. Clearing does not change capture level, restart endpoints, or alter
runtime behavior.

## Bytes warning

When the session capture level is `Bytes`, the panel displays a warning that
exact protocol frames may contain application payloads and that the display
filter does not change capture.

## Verification

Focused tests cover maximum-level exposure, available display levels,
cumulative filtering, hidden-record retention, selection reconciliation,
invalid filter rejection, clear behavior and command state, and Bytes warning
state.

No live capture-level mutation, endpoint restart, persistence, export,
northbound retrieval, or remote control is introduced.
