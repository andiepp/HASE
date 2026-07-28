# ADR-0036 Increment 3 — ByteArray Descriptor Serialization

## Scope

This increment carries `ByteArrayDataDescriptor` through the native HASE
Protocol Version 1 descriptor hierarchy.

It adds:

- data-descriptor discriminator `0x04` for ByteArray;
- direct descriptor encoding and decoding;
- ByteArray Property descriptor round-trip;
- nested endpoint, instrument, interface, Property, and data-descriptor
  round-trip; and
- serialization-model documentation.

## Encoding

ByteArray data descriptors contain no additional fields and are encoded as:

```text
Byte    Data descriptor discriminator = 0x04
```

The existing data-descriptor discriminator values remain unchanged:

| Value | Data descriptor |
| ---: | --- |
| `0x01` | String |
| `0x02` | Numeric |
| `0x03` | Boolean |
| `0x04` | ByteArray |

## Composition

The existing serializer composition remains unchanged:

```text
EndpointDescriptorSerializer
    → InstrumentDescriptorSerializer
        → InstrumentInterfaceSerializer
            → PropertyDescriptorSerializer
                → DataDescriptorSerializer
```

Every containing serializer remains independent of the concrete ByteArray
type. The extension is confined to `DataDescriptorSerializer`.

## Excluded work

This increment does not change:

- Property values, reads, writes, or observations;
- Command descriptors or execution;
- Compact Serial Protocol;
- gRPC contracts;
- WPF applications; or
- physical endpoint firmware.
