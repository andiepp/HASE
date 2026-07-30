# ADR-0040 Increment 40E4 — Production Byte Activation

## Scope

Activate the Native Protocol V1 and Compact Serial Protocol V1 byte observers
introduced by increments 40E2 and 40E3 at their production generation
boundaries.

This increment does not add UI, remote transport, persistence, export, or new
byte-retention policy.

## Opt-in activation

Production byte observation is activated only when the runtime diagnostic sink
enables `RuntimeDiagnosticLevel.Bytes` while a connection generation is
created.

Default, Operational-only, and Protocol-only configurations do not install a
production byte observer. Their Native and Compact receive paths therefore
retain the existing no-observer behavior and do not create diagnostic frame
copies.

## Native generation ownership

`RuntimeProtocolConnectionBinding` creates at most one
`NativeTransportByteDiagnosticObserver` for a duplex generation. It subscribes
the observer before starting the receive pump and removes it before cancelling
and observing that pump during replacement or disposal.

Legacy exchange-only transports remain unchanged. The duplex runtime adapter
and native diagnostic decorator preserve the optional
`ITransportByteTraceSource` capability without introducing it on connections
that do not provide it.

## Compact generation ownership

`CompactRuntimeProtocolDiagnosticConnection` creates at most one
`CompactTransportByteDiagnosticObserver` when its inner connection exposes
`ITransportByteTraceSource` and Bytes diagnostics are enabled. The decorator
removes that observer before disposing the inner connection.

The decorator preserves the optional byte-source and exchange-trace capability
shape, including connections that expose both capabilities.

## Identity and coexistence

Both observers use the authoritative endpoint identifier supplied by the
runtime coordinator. Native records retain protocol family `NativeProtocolV1`;
Compact records retain `CompactSerialProtocolV1`.

Bytes diagnostics coexist cumulatively with Operational and Protocol
diagnostics. Activation does not replace payload-free Protocol records, and one
observer per generation prevents duplicate byte records across reconnect
replacement.

## Verification

Focused tests cover:

- Native Bytes-enabled subscription and disposal removal;
- Native Protocol-only non-subscription;
- Compact Bytes-enabled publication, endpoint identity, protocol family, and
  disposal removal;
- Compact Protocol-only non-subscription; and
- optional byte capability preservation alongside existing notification and
  exchange-trace capabilities.

The repository-wide build and test pass remains the release gate.
