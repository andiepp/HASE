# ADR-0036 — Validation and Closure

## Outcome

ADR-0036 is implemented, automatically validated, remotely exercised across the
controlled private network, and accepted.

## Implemented scope

- Immutable `ByteArrayValue` and `ByteArrayDataDescriptor`.
- Protocol Version 1 Variant and descriptor serialization.
- Backward-compatible typed Command argument descriptor extensions.
- Runtime Command argument validation.
- Native, compact, northbound, protobuf, gRPC, and client mappings.
- WPF hexadecimal ByteArray argument editing and value presentation.
- Generic in-process endpoint attachment lifecycle.
- Opt-in Desktop Runtime Host ByteArray simulation.
- Snapshot, Property, Command, and observation integration.
- Command-line WPF client configuration using
  `laptop-private-network.json`.

## Validation endpoint

```text
EndpointId  : simulation-byte-buffer-validation
Instrument  : byte-buffer-01
Property    : Buffer.Value
Command     : Buffer.Replace
Argument    : Payload (ByteArray)
```

The validation confirmed transparent payload transport, exact returned bytes,
complete replacement semantics, authoritative cache update, remote Property
observation, stable client connectivity, and normal attachment ownership.

## Final baseline

```text
3,573 automated tests pass
.NET solution builds
Desktop and laptop private-network validation succeeds
```

ADR-0036 is closed.
