# ADR-0034 Increment 11 — Live Property Change Visualization

## Status

Implemented for validation.

## Scope

Existing instrument and Property ViewModels are now updated in place by stable
identity. Added and removed members are reconciled and descriptor ordering is
preserved.

A Property row is highlighted for approximately 1.5 seconds when its cached
known state, formatted value, quality, or UTC timestamp changes. A subsequent
change restarts the highlight period. Unchanged refreshes do not retrigger it.

No Property writes, authoritative reads, Commands, Events, runtime contracts,
transport behavior, or gRPC behavior are changed.
