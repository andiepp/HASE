# ADR-0043 — Increment 43B2 — Configuration-Driven Production Backend

## Status

Approved and implemented on 2026-08-01.

## Decision

The production Desktop Runtime Host consumes the endpoint composition and
installation identity resolved by the single-profile startup path.

Native-network attachment uses each configured expected endpoint identity,
host, and TCP port. Compact-serial discovery uses each configured USB vendor
and product identity, baud rate, verification timeout, and expected authoritative
endpoint identity. A compact candidate is attached only when discovery returns
exactly one verified endpoint and that identity matches the configured identity.

The required published endpoint count is calculated from the complete endpoint
composition plus the optional ByteArray simulation. The installation identity
file is passed to the existing file-backed identity resolver without a configured
fallback ID, allowing a generated installation-safe RuntimeHostId to be persisted.

The temporary Visual Studio compatibility route is projected into the historical
ESP32 and Arduino Uno configuration and retains its historical identity fallback.
Machine-specific constants no longer participate in the single-profile route.

Discovery, authoritative verification, explicit attachment, supervision,
recovery, detachment, disposal, mutual TLS, and diagnostics ownership remain
unchanged.
