# Project Status

## Current architectural work
**ADR-0042 — Laptop Client Diagnostics Window and Presentation Pause — In Progress**

### Current status
- ADR-0042 Increment 42A decision and client diagnostics boundary accepted
- ADR-0042 Increment 42B Client Diagnostics Capture Model accepted
- ADR-0042 Increment 42C Client Instrumentation accepted
- ADR-0042 42C baseline: 4,001 automated tests passing
- ADR-0042 Increment 42D Separate Diagnostics Window implemented
- The laptop application owns one bounded 2,000-record diagnostic collector
  and injects its publisher into the recovering gRPC session factory
- The main client window exposes `Open Diagnostics`
- One modeless diagnostics window exists at a time; repeated opening restores
  and activates the current window
- Closing Diagnostics leaves the client, connection, observation processing,
  and capture session running
- Filter, selection, records, and eviction accounting are owned by a singleton
  diagnostics view-model and therefore survive window close/reopen
- Main application exit closes the owned diagnostics window
- The diagnostics master/detail presentation shows sequence, UTC timestamp,
  level, category, event, severity, direction, outcome, correlation, duration,
  target identity, descriptor path, and structured metadata
- Level and category filters never change capture or discard retained records
- Clear affects only the client-local collector
- Automatic scrolling follows newly retained displayed records
- Presentation Pause/Resume remains deferred to Increment 42E
- Five focused tests cover main-window command delegation, projection,
  selection, filtering independence, clearing, eviction accounting, and
  deterministic metadata presentation

### Next
Build and run the complete automated suite, validate Increment 42D window
lifecycle, then implement Increment 42E after explicit approval.
