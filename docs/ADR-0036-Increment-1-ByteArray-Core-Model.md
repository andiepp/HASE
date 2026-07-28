# ADR-0036 Increment 1 — ByteArray Core Model

## Scope

This increment establishes ByteArray at the common model boundary.

It adds:

- `ByteArrayDataDescriptor` as the immutable descriptor for opaque binary data;
- `ByteArrayValue` as the immutable runtime representation;
- exact content and length equality;
- defensive copying at mutable array boundaries;
- explicit empty-value semantics; and
- a dedicated `Hase.Core.Tests` project.

## Semantics

`ByteArrayValue` preserves every byte exactly and assigns no meaning to its
contents.

The constructor copies a supplied mutable array. `ToArray` returns an
independent mutable copy. `AsSpan` provides read-only access without exposing a
mutable array.

An empty `ByteArrayValue` is a present zero-length value. It remains distinct
from null at Property and future Command boundaries.

## Excluded work

This increment does not change:

- Protocol Version 1 serialization;
- Compact Serial Protocol;
- Property operations or observations;
- Command descriptors or execution;
- gRPC contracts;
- WPF applications; or
- physical endpoint firmware.

Those boundaries follow in later ADR-0036 increments.
