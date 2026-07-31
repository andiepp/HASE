# ADR-0041 Increment 41G3 — Compact Serial Protocol V1 Byte Interpretation

## Scope

Add read-only structured interpretation for byte diagnostics identified as
`CompactSerialProtocolV1`.

## Inspection facade

`CompactSerialProtocolV1Inspection` is a narrow public read-only facade over
the existing internal Compact wire constants, message-type enum, correlation
rules, and `Crc16CcittFalse` calculation. It does not encode or decode frames
and owns no transport or connection lifecycle.

## Layout

- bytes 0-1: start marker `48 53`;
- byte 2: protocol version `01`;
- byte 3: message type;
- byte 4: correlation identifier;
- byte 5: payload length;
- bytes 6-n: opaque payload; and
- final two bytes: CRC-16/CCITT-FALSE, big-endian.

CRC coverage begins at version byte 2 and includes message type, correlation
identifier, payload length, and payload. It excludes the start marker and CRC.

## Validation

The interpreter validates marker, version, message type, correlation semantics,
declared frame boundary, and transmitted CRC. Event notifications require zero
correlation; request/response messages require nonzero correlation.

Largest valid Compact frames contain 263 bytes, seven more than the diagnostic
snapshot bound. A structurally consistent frame truncated by that bound marks
payload and CRC fields incomplete rather than invalid.

Payload bytes remain opaque and interpretation cannot affect Compact protocol
execution.
