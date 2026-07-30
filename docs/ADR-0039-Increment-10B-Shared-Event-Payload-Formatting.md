# ADR-0039 Increment 10B — Shared Event Payload Formatting

## Status

Implemented for validation.

## Scope

The new `Hase.Operator.Presentation` project owns descriptor-driven Event
payload formatting shared by operator applications.

`EventPayloadFormatter` accepts an optional `EventPayloadDescriptor` and an
optional normalized core value. It returns immutable text and a stable status
without throwing for endpoint-originated descriptor/value inconsistencies.

The supported payload types are:

- Boolean;
- Numeric;
- String; and
- ByteArray.

Boolean text is `True` or `False`. Numeric text is invariant and
round-trip-safe. Strings are preserved. ByteArrays use the established
uppercase hexadecimal presentation without separators.

Stable diagnostic results distinguish:

- no payload;
- a missing payload;
- an unexpected payload;
- a descriptor/value type mismatch; and
- an unsupported data descriptor.

## Verification

Automated coverage includes every status, every supported descriptor,
descriptor/value mismatches, non-finite numeric values, culture independence,
empty Strings and ByteArrays, hexadecimal formatting, immutable ByteArray
handling, and result-constructor validation.

This increment introduces the formatter only. The Laptop Client and Desktop
Runtime Host continue using their existing Event presentation until Increments
10C and 10D.
