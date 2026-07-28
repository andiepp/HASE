# ADR-0034 Increment 11 Fix 2 — Meaningful Property Highlight

## Scope

A timestamp-only cache refresh no longer restarts the Property-change
highlight.

The highlight now reacts only when one of these presentation-significant
fields changes:

- formatted value;
- quality; or
- known/unknown state.

The UTC timestamp continues to update normally, but recurring synchronization
of an unchanged value no longer keeps the Property card highlighted
indefinitely.

No runtime cache, Property read, Property write, Command, Event, transport, or
gRPC behavior changes.
