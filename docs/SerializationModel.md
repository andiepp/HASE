# Serialization Model

## Overview

HASE Protocol Version 1 uses a compact binary serialization model designed for:

- platform independence;
- embedded systems;
- deterministic encoding;
- forward compatibility;
- efficient transport over low-bandwidth links.

The serialization layer is completely independent of transport technology.

---

# Design Principles

The serialization architecture follows several principles.

## Explicit Encoding

Every protocol element has an explicitly defined binary representation.

The protocol never relies on platform-specific serialization mechanisms.

## Layered Composition

Serializers are composed from smaller reusable serializers.

```
EndpointDescriptorSerializer
        │
        ▼
InstrumentDescriptorSerializer
        │
        ▼
InstrumentInterfaceSerializer
        │
        ▼
PropertyDescriptorSerializer
        │
        ▼
DataDescriptorSerializer
```

The same approach is used for runtime values.

```
PropertyValueSerializer
        │
        ▼
VariantSerializer
```

This keeps every serializer focused on a single responsibility.

---

# Primitive Encoding

Protocol Version 1 uses little-endian encoding for numeric values.

Supported primitive encodings include:

- Boolean
- Byte
- UInt16
- Int32
- Int64
- Double
- UTF-8 String

Strings are encoded as:

```
UInt16 Length
UTF-8 Bytes
```

Optional strings are encoded using:

```
Presence Marker (0/1)
String
```

---

# Variant Serialization

Runtime values are transported using the VariantSerializer.

The protocol intentionally avoids CLR object serialization.

Protocol Version 1 currently supports:

| Variant Type | Description |
|--------------|-------------|
| Null | No value |
| Boolean | Boolean value |
| Int32 | 32-bit signed integer |
| Int64 | 64-bit signed integer |
| Double | IEEE-754 double precision |
| String | UTF-8 string |

Unsupported CLR types cause serialization to fail with a `NotSupportedException`.

Future protocol versions may extend the Variant type system while remaining backward compatible.

---

# Property Values

Runtime property values are serialized as:

```
Variant
Unix Timestamp (milliseconds)
Property Quality
```

The timestamp is represented as a signed 64-bit Unix timestamp in UTC.

Property quality is encoded as:

| Value | Quality |
|-------:|---------|
| 0 | Good |
| 1 | Uncertain |
| 2 | Bad |

---

# Descriptor Serialization

Descriptor serializers are hierarchical.

```
EndpointDescriptor
    │
    ▼
InstrumentDescriptor
    │
    ├── InstrumentMetadata
    └── InstrumentInterface
            │
            ├── PropertyDescriptor
            ├── CommandDescriptor
            └── EventDescriptor
```

Each serializer is responsible only for its own object and delegates nested objects to the appropriate serializer.

---

# Protocol Messages

Protocol messages are serialized by the `BinaryProtocolPayloadCodec`.

Message serialization is intentionally separated from domain object serialization.

```
BinaryProtocolPayloadCodec
        │
        ├── Request/Response framing
        ├── ProtocolResult
        └── Serializer composition
```

Domain serializers remain reusable outside the message layer.

---

# Error Handling

Malformed payloads result in `InvalidDataException`.

Unsupported runtime types result in `NotSupportedException`.

Unknown protocol enumeration values are treated as protocol errors and rejected during deserialization.

---

# Testing

Every serializer is verified through automated unit tests.

The test suite includes:

- binary layout verification;
- round-trip serialization;
- malformed payload detection;
- unsupported value handling;
- boundary conditions.

This ensures that the binary protocol remains stable as the implementation evolves.

---

# Versioning

Protocol Version 1 defines the binary format described in this document.

Future protocol versions may extend the protocol by introducing additional Variant types, message types or descriptor information while preserving backward compatibility whenever practical.