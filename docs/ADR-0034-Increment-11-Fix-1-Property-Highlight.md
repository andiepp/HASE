# ADR-0034 Increment 11 Fix 1 — Property Highlight Dispatcher Lifetime

## Scope

Replaces the asynchronous delay used by the Property-change highlight with a
WPF `DispatcherTimer`.

The highlighted state now starts, restarts, and clears on the UI dispatcher.
This avoids relying on an `async void` continuation for presentation state.

The fix does not change Property cache semantics.

A Command changes hardware state but does not automatically imply a cached
Property update. A Property row changes only after the runtime receives an
authoritative Property value, for example through a confirmation read or an
endpoint-originated Property notification.

No Property writes, authoritative polling, Commands, Events, runtime contracts,
transport behavior, or gRPC behavior are changed.
