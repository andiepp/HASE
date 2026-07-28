# ADR-0034 Increment 5 — Inventory Count Notification

## Status

Implemented for validation.

## Scope

This increment corrects the WPF published-endpoint counter.

`MainWindowViewModel` now implements `INotifyPropertyChanged` and raises a
notification for `PublishedEndpointCount` after each inventory refresh.

The endpoint collection and runtime projection behavior are unchanged.
