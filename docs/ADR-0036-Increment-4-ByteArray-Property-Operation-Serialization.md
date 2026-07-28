# ADR-0036 Increment 4 — ByteArray Property Operation Serialization

## Scope

This increment verifies that ByteArray values propagate through the complete
native Protocol Version 1 Property-operation serialization path.

It covers:

- timestamped and quality-qualified `PropertyValue` round-trip;
- empty ByteArray preservation distinct from null;
- successful `ReadPropertyResponse` round-trip;
- `WritePropertyRequest` round-trip; and
- endpoint-confirmed `WritePropertyResponse` round-trip.

## Architectural outcome

No production serializer changes are required.

The existing composition already provides the correct behavior:

```text
ReadPropertyResponse
    → PropertyValueSerializer
        → VariantSerializer

WritePropertyRequest
    → VariantSerializer

WritePropertyResponse
    → PropertyValueSerializer
        → VariantSerializer
```

Increment 2 added ByteArray to `VariantSerializer`. Every Property-operation
boundary delegates to that serializer and therefore carries ByteArray without
type-specific branching.

Adding duplicate ByteArray handling to the payload codec or Property-value
serializer would weaken the existing composition and is intentionally avoided.

## Semantics

Property timestamps and quality are preserved independently from the opaque
ByteArray payload.

An empty ByteArray remains a present `ByteArrayValue` with length zero. It does
not decode as null.

Confirmed Property writes preserve the authoritative ByteArray returned by the
endpoint.

## Excluded work

This increment does not change:

- runtime Property validation or caching;
- normalized northbound services;
- observation mapping;
- Command descriptors or execution;
- Compact Serial Protocol;
- gRPC contracts;
- WPF applications; or
- physical endpoint firmware.
