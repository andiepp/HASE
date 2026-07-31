# ADR-0041 Increment 41G1 — Structured Byte Interpretation Foundation

## Scope

Introduce the protocol-neutral, read-only presentation boundary for structured
interpretation of bounded byte diagnostics.

## Model

An interpreted field owns:

- byte offset;
- declared field length;
- field name;
- interpreted text;
- captured field bytes;
- spaced uppercase hexadecimal presentation; and
- validation state: not applicable, valid, invalid, or incomplete.

An interpretation result distinguishes recognized-valid,
recognized-malformed-or-incomplete, unsupported protocol family, and no
captured bytes.

## Dispatch and safety

`DesktopRuntimeByteInterpretationService` dispatches the existing exact
`protocolFamily` diagnostic detail to one registered read-only interpreter.
Registration is ordinal and rejects duplicates.

No interpreter may participate in transport or protocol execution. The service
operates only on the immutable bounded `RuntimeDiagnosticByteSnapshot`. An
interpreter exception or null result is converted into a safe malformed result
and never propagates into the diagnostics presentation.

## Deferred

No protocol-family interpreter, diagnostic-entry integration, or WPF change is
included. Native Protocol V1 belongs to 41G2, Compact Serial Protocol V1 to
41G3, and diagnostics-window presentation to 41G4.
