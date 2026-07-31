# Project Status

## Current architectural work
**ADR-0042 — Laptop Client Diagnostics Window and Presentation Pause — In Progress**

### Current status
- ADR-0042 Increments 42A through 42D accepted
- ADR-0042 42D baseline: 4,006 automated tests passing
- ADR-0042 Increment 42E Presentation Pause/Resume implemented
- Pause freezes projected records, selection, and automatic scrolling only
- Diagnostic capture, sequence assignment, bounded retention, and eviction
  continue while presentation is paused
- Running/Paused state and the count of retained records awaiting reconciliation
  are shown explicitly
- Filter changes while paused apply to the frozen presentation source without
  admitting newly captured records
- Resume replaces the frozen source with the collector's current retained
  snapshot; records evicted during pause do not reappear
- Clear while paused clears the collector and frozen projection, resets pending
  and eviction display, and preserves Paused state
- Pause state, filter, selection, and projection remain owned by the singleton
  diagnostics view-model and survive window close/reopen
- A new application process begins in Running state
- Pause/Resume does not control transport, connection, observation processing,
  Property operations, Commands, or Events
- Six focused tests cover pause, continued capture, pending records, resume,
  bounded eviction, clear while paused, filtering, and session-state ownership

### Next
Build and run the complete automated suite, validate Increment 42E behavior,
then define Increment 42F structured northbound presentation.
