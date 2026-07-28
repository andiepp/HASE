# ADR-0034 Increment 10 — Read-Only Property Projection

## Status

Implemented for validation.

## Scope

This increment projects each instrument's Property descriptors and current
runtime cache entries into the Desktop Runtime Host operator UI.

Each Property row displays:

- display name;
- descriptor path;
- access mode;
- current cached value;
- quality; and
- UTC timestamp.

The projection uses `IRuntimeHostPropertyService.GetCached`. It performs no
endpoint communication and does not write values. Unknown cache entries are
shown explicitly as `Unknown`.

Property values are formatted with invariant culture. Timestamps use the
round-trip UTC representation.

Writable Property editors, authoritative reads, Commands, and Events remain
outside this increment.

No transport, attachment, supervision, protocol, northbound contract, or gRPC
behavior is changed.
