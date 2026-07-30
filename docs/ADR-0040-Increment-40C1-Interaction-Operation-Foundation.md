# ADR-0040 Increment 40C1 — Interaction Operation Foundation

## Decision

Introduce a UI-neutral `RuntimeDiagnosticOperation` helper for one correlated
operational interaction.

The helper:

- publishes one `Started` record at construction;
- preserves a stable operation identifier across the terminal record;
- measures duration with a monotonic `TimeProvider`;
- publishes `Completed` only for `Succeeded`;
- publishes `Failed` for `Failed`, `Cancelled`, and `TimedOut`;
- assigns information severity to success and warning severity to non-success;
- publishes at most one terminal record;
- rethrows the original operation exception unchanged; and
- relies on `RuntimeDiagnosticPublisher` to isolate diagnostic observer failure.

## Privacy boundary

The foundation carries only caller-supplied structural identity details.
Callers must not supply property values, write values, command arguments,
event payloads, byte arrays, raw payloads, exception messages, or stack traces.
The operation helper does not inspect results or exceptions.

## Scope boundary

This increment adds the shared operation primitive and its tests only.
Property, command, and event production ports remain unchanged until 40C2,
40C3, and 40C4 respectively.

## Validation

The focused tests cover correlation, identity, monotonic duration, terminal
idempotence, stable outcome classification, unchanged exception propagation,
privacy, and observer isolation.
