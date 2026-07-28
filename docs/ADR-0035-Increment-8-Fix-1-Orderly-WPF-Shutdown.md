# ADR-0035 Increment 8 Fix 1 — Orderly WPF Shutdown

## Status

Implemented for validation.

## Problem

Physical validation confirmed correct live Arduino and ESP32 Event projection,
but closing the Desktop Runtime Host window did not terminate the application.

`App.OnExit` performs the required synchronous bridge into asynchronous runtime
shutdown on the WPF dispatcher thread. The Increment 8 Event observation task
captured that dispatcher while awaiting observation delivery and cancellation.
Shutdown then blocked the dispatcher while waiting for an observation
continuation that required the blocked dispatcher.

## Resolution

The Event observation and shutdown coordination no longer capture the WPF
synchronization context while:

- cancelling the Event subscription;
- awaiting the Event observation task;
- enumerating normalized observations;
- awaiting a queued dispatcher update; or
- stopping the runtime host.

Occurrence collection mutation still executes explicitly on the WPF dispatcher.
The subscription is still cancelled and awaited before the production runtime
backend is stopped and disposed.

This fix changes no Event content, retention policy, source attribution,
protocol, runtime contract, or gRPC behavior.
