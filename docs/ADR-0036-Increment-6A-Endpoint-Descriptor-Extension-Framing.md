# ADR-0036 Increment 6A — Endpoint Descriptor Extension Framing

## Scope

This increment establishes generic Protocol Version 1 framing for optional
endpoint descriptor extensions.

It adds:

- a stable one-byte extension type;
- a UInt16 extension count;
- UInt16 length-delimited extension payloads;
- preservation of unknown extension types at the framing boundary;
- defensive payload copying;
- non-empty section and payload rules; and
- malformed, oversized, and truncated payload rejection.

## Encoding

An extension section is encoded as:

```text
UInt16  Extension count

For each extension:
    Byte    Extension type
    UInt16  Payload length
    Byte[]  Payload
```

The section is optional at the endpoint-descriptor message boundary. A writer
must not emit an empty section. This preserves the exact legacy endpoint
descriptor bytes whenever no extensions exist.

## Framing semantics

The generic framing layer does not interpret extension types or payloads.

It preserves:

- extension order;
- the exact type byte; and
- the exact payload bytes.

Known extension interpretation and unknown-type skipping belong to the
endpoint-descriptor extension mapping layer introduced by the next increment.

Each payload must contain at least one byte and must not exceed 65,535 bytes.
The serializer rejects rather than truncates oversized values.

## Compatibility

This increment does not yet append extension sections to endpoint descriptors.
It only establishes and tests the generic framing component.

Existing endpoint descriptor serialization therefore remains unchanged.

## Excluded work

This increment does not add:

- the Command argument extension type;
- endpoint descriptor integration;
- Command argument reconstruction;
- runtime Command validation;
- normalized or gRPC mapping; or
- endpoint firmware changes.
