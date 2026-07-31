# ADR-0041 — Physical Validation and Closure

## Baseline

- Date: 2026-07-31
- Automated tests: 3,981 passing
- Native endpoint: DOIT ESP32 DevKitC V4
- Compact endpoint: Arduino Uno

## Window and presentation validation

Validation confirmed modeless positioning, one-window activation, minimize and
restore, independent close/reopen, owner shutdown, and fresh Running state after
application restart. Paused state, filter, frozen records, and selection survive
window recreation. Capture continues while paused and closed; Resume reconciles
the current bounded snapshot.

## Capture-level validation

- Operational: 12 checks passed.
- Protocol: 12 checks passed.
- Bytes: 17 checks passed.
- Combined: 41 checks passed.

Both physical endpoint families remained operational. Cumulative filtering did
not change capture. Clear worked while running and paused.

## Structured-byte validation

### Native Protocol V1

Eleven checks passed across request, response, and EventNotification records.
Version, role, message type, correlation ID, payload length, payload boundary,
direction, and raw offsets agreed. Notifications used correlation zero.

### Compact Serial Protocol V1

Twelve checks passed across request, response, and EventNotification records.
Marker `48 53`, version, message type, correlation ID, payload length, payload,
and CRC fields agreed with raw bytes. Transmitted and calculated CRC values
matched. Notifications used correlation zero and request/response traffic used
nonzero correlation.

## Result

ADR-0041 is accepted and complete. No defect or production correction remained
after final physical validation.
