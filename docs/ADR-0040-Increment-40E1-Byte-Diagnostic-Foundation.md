# ADR-0040 Increment 40E1 — Byte Diagnostic Foundation

## Scope

Introduce the UI-neutral, bounded, immutable record foundation for exact
transport-byte diagnostics. This increment adds no Native or Compact
integration and no production activation.

## Snapshot contract

`RuntimeDiagnosticByteSnapshot` owns a copied byte snapshot and records:

- original byte count;
- captured byte count;
- truncation status; and
- the captured bytes.

Every record captures at most 256 bytes. Original and captured counts must be
non-negative and consistent, captured count cannot exceed original count, and
truncation must exactly match whether bytes were omitted.

The public byte view is read-only. `ToArray` returns a new copy.

## Publication

`RuntimeTransportByteDiagnosticPublisher` publishes:

- `TransportBytesSent` for outbound bytes; and
- `TransportBytesReceived` for inbound bytes.

Records use `RuntimeDiagnosticLevel.Bytes` and
`RuntimeDiagnosticCategory.TransportBytes`. They carry authoritative endpoint
identity, protocol family, optional correlation identifier, direction, byte
counts, truncation status, and the immutable snapshot.

The bytes factory is not evaluated unless the configured sink enables the
cumulative `Bytes` level. Diagnostic sink failures remain isolated by
`RuntimeDiagnosticPublisher`.

## Security boundary

Exact bytes are sensitive local diagnostic data. This increment adds no
logging, persistence, export, remote retrieval, northbound enablement, or user
interface.

## Verification

Focused tests cover complete and truncated captures, immutable ownership,
cumulative-level behavior, disabled zero-evaluation behavior, metadata and
direction validation, fixed-bound enforcement, and throwing-sink isolation.

Native integration remains deferred to Increment 40E2.
