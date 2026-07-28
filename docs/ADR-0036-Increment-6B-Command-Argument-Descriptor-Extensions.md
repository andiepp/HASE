# ADR-0036 Increment 6B — Command Argument Descriptor Extensions

## Scope

This increment integrates typed Command argument descriptors with native HASE
Protocol Version 1 endpoint descriptor responses.

It adds:

- extension type `0x01` for one required Command argument;
- deterministic extension creation in instrument and Command order;
- argument descriptor payload serialization;
- endpoint-level extension section emission only when typed Commands exist;
- typed Command reconstruction after legacy endpoint descriptor decoding;
- unknown extension skipping; and
- strict duplicate, target, and payload validation.

## Command argument extension payload

Extension type `0x01` contains:

```text
String          InstrumentId
String          Command path
String          Argument display name
OptionalString  Argument description
DataDescriptor  Argument data
```

The generic extension framing supplies the payload length.

## Encoding flow

Typed endpoint descriptor responses are encoded as:

```text
Protocol result
Descriptor-present marker
Legacy EndpointDescriptor
Endpoint descriptor extension section
```

The legacy descriptor serializes every Command using the established
parameterless representation. The extension section then identifies which
Commands require typed arguments.

If the endpoint contains no typed Commands, no extension section is emitted.
Its response payload remains byte-for-byte identical to the legacy encoding.

## Decoding flow

The decoder:

1. decodes the complete legacy endpoint descriptor;
2. checks whether unread payload bytes remain;
3. decodes the length-delimited extension section when present;
4. skips unknown extension types;
5. resolves each known extension by InstrumentId and Command path;
6. reconstructs the immutable typed `CommandDescriptor`; and
7. verifies that the complete response payload was consumed.

## Rejection rules

The decoder rejects:

- duplicate argument extensions for the same target;
- an unknown target instrument;
- an unknown target Command;
- an argument applied to a Command that already has one;
- malformed known argument data descriptors;
- a known extension payload with trailing bytes; and
- malformed generic extension framing.

Typed Commands are never silently downgraded to parameterless Commands by a
compatible reader.

An older reader rejects a typed endpoint response because the extension section
appears as unexpected trailing bytes. Existing parameterless endpoints remain
fully compatible.

## Excluded work

This increment does not add:

- runtime argument validation;
- normalized Command argument contracts;
- northbound gRPC argument mapping;
- client argument editors;
- simulated typed-Command execution; or
- physical endpoint firmware changes.
