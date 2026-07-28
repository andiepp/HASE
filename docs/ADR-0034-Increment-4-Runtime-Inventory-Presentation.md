# ADR-0034 Increment 4 — Runtime Inventory Presentation

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host now projects the authoritative in-process runtime-host
snapshot into the WPF operator window.

The projection displays:

- published endpoint count;
- endpoint display name;
- authoritative endpoint identity;
- current connection state; and
- opaque attachment generation.

The desktop application does not connect to its own gRPC API. The production
backend implements a presentation-neutral inventory-source contract by capturing
the existing immutable northbound snapshot from the in-process composition.

A WPF dispatcher timer refreshes the projection once per second. Collection
updates therefore occur on the UI thread. Refresh failures are observational and
do not terminate the runtime-host process.

This increment does not add endpoint commands, Property access, attachment
controls, diagnostics, or event history.
