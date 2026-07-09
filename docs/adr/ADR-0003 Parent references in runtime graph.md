# ADR-0001 - Self Describing Instruments

## Status

Accepted

## Decision

Every HASE instrument shall describe itself at runtime.

The descriptor shall expose:

- Metadata
- Properties
- Commands
- Events
- Capabilities

Applications shall not require prior knowledge of a specific instrument.

## Consequences

A generic application (e.g. HASE Studio) can discover and operate previously unknown instruments.

Instrument-specific applications may provide additional strongly typed wrappers.