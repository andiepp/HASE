# ADR-0036 Increment 2 — Protocol V1 ByteArray Serialization

## Scope

This increment carries the ADR-0036 ByteArray value through the native HASE
Protocol Version 1 Variant encoding.

It adds:

- Variant discriminator `0x06` for ByteArray;
- UInt16 little-endian byte-length encoding;
- exact opaque byte payload preservation;
- empty ByteArray encoding;
- the existing Protocol Version 1 maximum of 65,535 bytes;
- oversized-value rejection before payload bytes are written;
- truncated-payload rejection; and
- focused compatibility and boundary tests.

## Encoding

ByteArray is encoded as:

```text
Byte    Variant discriminator = 0x06
UInt16  Byte length
Byte[]  Exact payload bytes
```

The length uses the existing Protocol Version 1 UInt16 little-endian encoding.

The following existing discriminator values remain unchanged:

| Value | Variant |
| ---: | --- |
| `0x00` | Null |
| `0x01` | Boolean |
| `0x02` | Int32 |
| `0x03` | Int64 |
| `0x04` | Double |
| `0x05` | String |
| `0x06` | ByteArray |

## Boundary semantics

An empty ByteArray is encoded as discriminator `0x06` followed by a zero
length. It is distinct from Null, which remains discriminator `0x00`.

The serializer accepts only `ByteArrayValue`. A mutable CLR `byte[]` is not
implicitly accepted or converted.

Payloads longer than 65,535 bytes are rejected. Payloads declaring more bytes
than remain in the message are malformed and rejected.

## Excluded work

This increment does not change:

- descriptor serialization;
- Property operations or observations;
- Command descriptors or execution;
- Compact Serial Protocol;
- gRPC contracts;
- WPF applications; or
- physical endpoint firmware.
