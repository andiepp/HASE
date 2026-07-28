# ADR-0034 Increment 7 — Endpoint-State Visualization

## Status

Implemented for validation.

## Scope

This increment adds presentation-only visual state indicators to persistent
endpoint rows.

The endpoint view model now classifies these authoritative runtime states:

- `Ready`;
- recovering states: `Connecting`, `Synchronizing`, and `Reconnecting`;
- `Faulted`; and
- `Disconnected`.

The WPF endpoint cards use data triggers to distinguish these states through
border, background, indicator text, and foreground styling.

No runtime, transport, attachment, supervision, snapshot, or gRPC behavior is
changed.
