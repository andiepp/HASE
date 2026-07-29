# ADR-0036 — ByteArray Values and Typed Command Arguments

## Status

Accepted and implemented.

## Context

HASE Protocol Version 1 originally supported scalar runtime values and
parameterless Commands. Applications also require transparent binary payloads
whose structure and interpretation belong exclusively to the application and
the endpoint counterparts.

Typed Command arguments must remain descriptor-driven, transport-independent,
and compatible with existing parameterless Commands.

## Decision

### ByteArray value model

`ByteArrayValue` is an immutable opaque ordered sequence of bytes.

- HASE does not interpret its contents.
- Construction and retrieval use defensive copies.
- Empty ByteArray and null are distinct values.
- Equality and hashing are content based.
- Mutable CLR `byte[]` is not accepted implicitly.

`ByteArrayDataDescriptor` declares ByteArray values without adding
application-specific schema.

### Protocol Version 1

ByteArray uses stable Variant discriminator `0x06`, followed by a little-endian
`UInt16` length and the exact bytes. The maximum payload is therefore 65,535
bytes.

Property descriptor discriminator `0x04` identifies
`ByteArrayDataDescriptor`.

Typed Command argument metadata is carried by a backward-compatible descriptor
extension. Existing Commands without arguments retain their original encoding.
Unknown extension fields are skipped by length and malformed extensions are
rejected.

Command requests and successful results carry ByteArray through the existing
Variant boundary.

### Runtime and northbound services

The runtime validates Command arguments against the immutable Command
descriptor before execution. Parameterless Commands continue to require null.

ByteArray is mapped explicitly through normalized Property and Command
operations, protobuf contracts, gRPC adapters, client contracts, snapshots,
and live Property observations.

### Applications

The WPF Laptop Client displays typed Command metadata and accepts ByteArray as
complete hexadecimal byte pairs with optional whitespace. Invalid text remains
local and cannot execute.

The Desktop Runtime Host can opt into a validation endpoint using:

```text
--include-byte-buffer-simulation
```

Without the switch, the existing two-physical-endpoint startup is unchanged.
With the switch, `simulation-byte-buffer-validation` is attached through the
normal host-owned inventory and lifecycle.

Its instrument contract is:

```text
Endpoint   : simulation-byte-buffer-validation
Instrument : byte-buffer-01
Property   : Buffer.Value       (read-only ByteArray)
Command    : Buffer.Replace     (Payload: ByteArray)
```

`Buffer.Replace` atomically replaces the complete value, returns the accepted
bytes, updates the normal runtime Property cache, and produces the normal
Property observation.

## Consequences

- Binary payloads remain opaque and portable.
- Existing parameterless Commands and physical endpoint behavior remain
  compatible.
- Descriptor metadata is authoritative for application editors and runtime
  validation.
- Simulated endpoints use the same attachment, inventory, generation,
  northbound, gRPC, observation, and shutdown boundaries as physical endpoints.
- ByteArray is not a substitute for a future structured binary schema.

## Validation

Validation completed with:

```text
3,573 automated tests passing
.NET solution builds
Desktop Runtime Host publishes two physical endpoints by default
Opt-in host publishes the ByteArray simulation as the third endpoint
WPF Laptop Client starts from laptop-private-network.json
Buffer.Replace accepts hexadecimal ByteArray input
Accepted bytes are returned unchanged
Buffer.Value is replaced rather than appended
ByteArray Property observation reaches the remote client without disconnect
Client and Desktop Runtime Host display the resulting hexadecimal value
Orderly application and attachment shutdown remains intact
```

## Evolution in ADR-0037

ADR-0036 originally validated `Buffer.Value` as a read-only ByteArray Property.
ADR-0037 later made it writable and expanded the same instrument with writable
Boolean, Numeric, and String Properties. `Buffer.Replace` remains compatible
and updates the same authoritative ByteArray Property.
