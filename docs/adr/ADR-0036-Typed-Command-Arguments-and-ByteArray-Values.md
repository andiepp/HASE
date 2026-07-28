# ADR-0036 — Typed Command Arguments and ByteArray Values

## Status

Accepted

## Context

HASE currently executes parameterless Commands through the normalized runtime,
the versioned northbound gRPC API, the Desktop Runtime Host, and the Laptop
Client. The current `CommandDescriptor` does not describe an argument.

HASE also supports Null, Boolean, Int32, Int64, Double, and String runtime
values. Applications and endpoints additionally require an opaque binary value
whose structure is not interpreted by HASE.

## Decision

HASE will support descriptor-driven typed Command arguments.

The descriptor distinguishes:

- a parameterless Command, for which the argument is null; and
- a Command with one required typed argument.

Optional arguments, multiple arguments, structured arguments, and Command
result values are excluded from this decision.

HASE will also support ByteArray as a first-class Property and Command argument
value type.

ByteArray is an opaque ordered sequence of bytes:

- all byte values from `0x00` through `0xFF` are valid;
- byte order and length are preserved exactly;
- no text encoding, schema, MIME type, or application structure is implied;
- HASE does not automatically convert ByteArray and String values;
- an empty ByteArray is a present value containing zero bytes;
- null is distinct from an empty ByteArray;
- ByteArray values are immutable at public boundaries; and
- equality is based on byte length and byte content.

The application and endpoint counterparts exclusively own the interpretation
of ByteArray contents.

## Validation ownership

Clients may validate argument entry for usability.

The runtime host remains authoritative. Before dispatch it resolves the
Command descriptor and validates whether an argument is permitted, required,
and of the exact described type.

Endpoint adapters encode an already validated value. They do not silently
convert, truncate, or reinterpret it.

## Representation

Each boundary uses its native binary representation:

- the common model uses an immutable ByteArray value;
- Protocol Version 1 uses a length followed by the exact bytes;
- Protobuf uses `bytes`;
- JSON uses Base64 only as a boundary representation; and
- operator applications use hexadecimal for display and entry.

Protocol framing limits remain authoritative. A future descriptor may add an
optional maximum ByteArray length. No layer silently truncates an oversized
value.

## Compatibility

Existing parameterless Commands continue to carry null and remain wire
compatible.

ByteArray receives a new protocol value discriminator. Existing discriminator
values are not changed.

## Implementation sequence

1. Common ByteArray descriptor and immutable value semantics.
2. Protocol Version 1 descriptor and value serialization.
3. ByteArray Property operations and observations.
4. Typed Command argument descriptor semantics.
5. Runtime argument validation and dispatch.
6. Northbound gRPC contract and mapping.
7. Client contracts and hexadecimal operator editing.
8. Simulated end-to-end validation.
9. Physical validation where a concrete endpoint capability justifies it.

## Consequences

HASE gains transparent binary values without treating encoded binary text as a
String.

Typed Commands remain descriptor-driven and transport-independent.

Large-value streaming, file transfer, schemas, compression, partial updates,
and typed Command results remain separate architectural concerns.
