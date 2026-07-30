# ADR-0040 Increment 40F2 — Diagnostic Record Projection

## Scope

Add deterministic, UI-neutral projection of structured runtime diagnostics for
the Desktop Runtime Host without adding WPF presentation or controls.

## Presentation entry

`DesktopRuntimeDiagnosticEntry` is an immutable presentation record containing:

- process-local sequence and UTC timestamp;
- level, category, event name, and severity;
- endpoint and attachment-generation identity;
- direction, operation identity, duration, and outcome;
- an immutable ordered detail collection; and
- bounded byte-snapshot metadata and hexadecimal text.

Absent optional record fields project to empty display text. Numeric byte counts
remain zero when no byte snapshot exists, paired with `HasByteSnapshot` to
distinguish absence from an empty captured frame.

## Deterministic formatting

`DesktopRuntimeDiagnosticEntryProjector`:

- normalizes timestamps to UTC and formats them with invariant round-trip
  precision;
- formats GUID identities in canonical `D` form;
- formats duration with the invariant constant `TimeSpan` representation;
- orders detail keys using ordinal comparison;
- renders captured bytes as uppercase hexadecimal without decoding them; and
- reports captured count, original count, and truncation.

The projector creates its own read-only detail collection. Later source
mutation cannot change the projected entry.

## Safety boundary

Projection uses only fields already admitted by the ADR-0040 diagnostic
envelope. It does not add exception text, stack traces, network addresses,
ports, COM names, credentials, configuration paths, decoded application
values, or unbounded byte content.

Projection is observational. It does not publish diagnostics, read endpoint
caches, influence runtime behavior, persist records, or expose them through the
northbound API.

## Verification

Focused tests cover complete Operational records, absent optional fields,
canonical generation and operation identity, invariant UTC and duration text,
ordinal immutable details, complete and truncated byte snapshots, uppercase
hexadecimal output, source immutability, and null rejection.

WPF collection management, polling, filtering, selection, and layout remain
deferred to Increment 40F3.
