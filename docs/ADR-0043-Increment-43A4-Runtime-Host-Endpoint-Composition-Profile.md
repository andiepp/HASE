# ADR-0043 — Increment 43A4 — Runtime Host Endpoint Composition Profile

## Status

Approved and implemented on 2026-08-01.

## Decision

The Desktop Runtime Host endpoint composition is represented by immutable,
UI-independent configuration contracts in `Hase.DesktopHost` and loaded from a
bounded, versioned, strict external JSON file.

Version 1 supports the two production attachment families already owned by the
Desktop Runtime Host:

- Native Protocol Version 1 over a configured network host and TCP port.
- Compact Serial Protocol Version 1 selected by USB vendor/product identity,
  baud rate, verification timeout, and authoritative expected endpoint identity.

Expected endpoint identities must be unique across the complete composition.
The file reader rejects unknown properties, unsupported endpoint kinds,
kind-inappropriate properties, unsupported versions, invalid ranges, duplicate
identities, empty compositions, oversized documents, and relative top-level
paths.

The profile contains no credentials and performs no discovery, verification,
attachment, replacement, or endpoint lifecycle operation. Those responsibilities
remain with the existing Runtime Host composition.

## Consequence

Increment 43B can adapt the existing production backend to a single application
profile path while preserving explicit endpoint identity validation and Runtime
Host lifecycle ownership. Further endpoint families require an explicit schema
and architecture extension.
