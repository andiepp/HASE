# ADR-0041 Increment 41G4 — Structured Byte Presentation Integration

## Scope

Integrate the Native Protocol V1 and Compact Serial Protocol V1 read-only byte
interpreters into Desktop Runtime Host diagnostic projection and presentation.

## Integration

One default `DesktopRuntimeByteInterpretationService` registers both approved
interpreters. The application container supplies that shared service to the
singleton `RuntimeDiagnosticsViewModel`.

Projection reads the existing ordinal `protocolFamily` diagnostic detail and
passes the immutable `RuntimeDiagnosticByteSnapshot` directly to the service.
It never reparses the formatted hexadecimal text.

`DesktopRuntimeDiagnosticEntry` preserves constructor compatibility and adds an
immutable interpretation result with convenience presentation properties.

## Presentation

The selected byte record retains its raw captured-byte summary and hexadecimal
text. Beneath it, Structured interpretation presents protocol family, status,
summary, and a read-only table containing offset, length, field, interpreted
value, corresponding bytes, and validation.

Raw bytes remain visible for valid, invalid, incomplete, unsupported, and
future protocol families.

## Behavior

Interpretation is part of immutable record projection. Existing filtering,
selection, clearing, pause/resume, close/reopen, and newest-first behavior is
unchanged. No transport, capture, protocol execution, or runtime authority is
added.
