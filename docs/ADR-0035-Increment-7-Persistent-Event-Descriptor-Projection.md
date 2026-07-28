# ADR-0035 Increment 7 — Persistent Event Descriptor Projection

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host operator console now projects the Event descriptors
published by each instrument.

Each Event snapshot and ViewModel contains:

- descriptor path;
- display name; and
- optional description.

The production inventory source captures Event descriptors from the same
authoritative endpoint descriptor used for Property and Command projection.
The WPF instrument presentation displays the read-only Event collection after
its Properties and Commands.

Event ViewModels are persistent across ordinary inventory refreshes. Reconciliation:

- identifies an Event by its descriptor path;
- preserves the existing ViewModel when immutable metadata is unchanged;
- replaces the ViewModel when display name or description changes;
- adds and removes descriptors with the authoritative inventory; and
- preserves authoritative descriptor ordering.

## Verification

Automated coverage includes:

- descriptor metadata projection;
- persistent identity for unchanged descriptors;
- replacement after immutable metadata changes;
- addition, removal, count, and ordering;
- empty-path rejection; and
- empty-display-name rejection.

This increment projects descriptors only. It introduces no observation
subscription, live Event occurrence, replay, acknowledgement, persistence,
filtering, export, protocol change, runtime contract change, or gRPC change.
Live Event occurrences remain ADR-0035 Increment 8.
