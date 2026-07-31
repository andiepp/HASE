# ADR-0041 — Desktop Diagnostics Window and Presentation Pause

- Status: Accepted
- Date: 2026-07-31

## Context

ADR-0040 established bounded, process-local Operational, Protocol, and Bytes
diagnostics and presented them inside the Desktop Runtime Host main window.
Operators need to position diagnostics beside the host UI, freeze presentation
without interrupting capture, and interpret exact Native and Compact frames
without manually counting raw hexadecimal offsets.

## Decision

### Separate modeless window

The main host window exposes `Open Diagnostics`. One modeless diagnostics window
exists at a time. Repeated invocation restores and activates the existing
window. Closing Diagnostics leaves the host and capture session running.

The diagnostics window is owned by the main host window. Closing the host closes
Diagnostics and permits deterministic process exit.

The application-session singleton `RuntimeDiagnosticsViewModel` is independent
of window instances. Closing and reopening preserves presentation state. A new
application process creates a fresh Running state.

### Presentation pause

Pause freezes presentation reconciliation only. Diagnostic publication,
bounded source retention, sequence assignment, and eviction continue. The
frozen projected records, display filter, and selection remain available.

Resume immediately reconciles the current retained snapshot. Records evicted
while paused do not reappear. Clear remains an explicit action and clears both
the source and frozen projection while preserving paused state.

### Structured byte interpretation

Raw hexadecimal bytes remain visible. Selected Bytes records additionally show
protocol family, interpretation status, summary, and immutable fields containing
offset, length, field name, interpreted value, corresponding bytes, and
validation state.

Native Protocol V1 traces begin with the protocol envelope, not the framed-TCP
length delimiter. Interpretation covers major/minor version, role, message type,
little-endian correlation ID, little-endian payload length, and opaque payload.

Compact Serial Protocol V1 interpretation covers marker `48 53`, version,
message type, correlation ID, payload length, opaque payload, and big-endian
CRC-16/CCITT-FALSE. CRC calculation delegates to the existing Compact
implementation through a narrow read-only inspection facade.

Interpretation consumes only immutable bounded diagnostic snapshots. It cannot
encode, decode for execution, change connections, mutate runtime state, or
affect capture. Unsupported, malformed, incomplete, truncated, failed, and
no-byte cases become safe presentation results.

## Consequences

- Diagnostics can be positioned independently of the host UI.
- Window lifecycle no longer defines presentation-session lifecycle.
- Operators can inspect a stable display while physical activity continues.
- Raw bytes remain available for authoritative comparison.
- Native and Compact framing can be interpreted without duplicating Compact CRC
  rules or participating in the live protocol path.
- Bytes capture remains startup-owned and may expose application payloads.
- No diagnostic northbound contract is added.

## Implemented increments

1. 41A — Diagnostics Window Lifecycle Foundation.
2. 41B — Separate Diagnostics Presentation.
3. 41C — Presentation Pause State.
4. 41D — Pause/Resume Controls and Status.
5. 41E — Lifecycle and Presentation Closure.
6. 41F — Combined Physical Validation.
7. 41G1 — Structured Byte Interpretation Foundation.
8. 41G2 — Native Protocol V1 Byte Interpretation.
9. 41G3 — Compact Serial Protocol V1 Byte Interpretation.
10. 41G4 — Structured Byte Presentation Integration.
11. 41G5 — Structured Byte Physical Validation.
12. 41H — Documentation and Closure.

## Validation

- 3,981 automated tests pass.
- Operational, Protocol, and Bytes capture levels were physically validated.
- ESP32 Native Protocol V1 and Arduino Uno Compact Serial Protocol V1 remained
  operational through filtering, clearing, Pause/Resume, close/reopen, and host
  shutdown.
- Forty-one combined 41F capture-level checks passed.
- Eleven final Native structured-byte checks passed.
- Twelve final Compact structured-byte checks passed.
- Native and Compact request, response, and notification records agreed with raw
  bytes, payload boundaries, correlation rules, and Compact CRC calculations.

## Deferred

- live capture-level changes;
- persistent diagnostic storage and rotation;
- export and search;
- semantic payload decoding;
- northbound diagnostics retrieval and control; and
- external observability integration.
