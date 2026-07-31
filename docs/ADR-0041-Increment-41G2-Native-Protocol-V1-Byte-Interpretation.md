# ADR-0041 Increment 41G2 — Native Protocol V1 Byte Interpretation

## Scope

Add read-only structured interpretation for byte diagnostics whose existing
protocol-family discriminator is `NativeProtocolV1`.

## Capture boundary

`ProtocolDuplexSession` traces the complete encoded Protocol V1 envelope after
framed TCP has removed its transport delimiter. The captured array therefore
does not contain a TCP length prefix.

The interpreted layout exactly follows `ProtocolEnvelopeByteCodec`:

- byte 0: major version;
- byte 1: minor version;
- byte 2: message role;
- byte 3: message type;
- bytes 4-7: correlation identifier, little-endian;
- bytes 8-11: payload length, little-endian; and
- bytes 12-n: opaque payload body.

## Validation

The interpreter uses `ProtocolVersion.Current`, `ProtocolMessageRole`, and
`ProtocolMessageType`. It validates header availability and the declared payload
boundary against the original frame length.

A structurally consistent frame truncated only by the 256-byte diagnostic bound
keeps a valid payload-length field and marks the payload field incomplete.
Undefined enums, unsupported versions, impossible lengths, trailing bytes, and
declared payload beyond the original frame are invalid.

Payload bytes remain opaque. Semantic Property, Command, Event, and descriptor
decoding is not introduced.

## Safety

Interpretation consumes only the immutable diagnostic snapshot and has no
authority over protocol decoding, transport framing, connection state, or
runtime behavior.
