# ADR-0040 Increment 40C4 — Event Occurrence Diagnostics

## Decision

Instrument `RuntimeEvent.PublishOccurrence`, the shared runtime boundary for
one Event occurrence.

Each occurrence publishes exactly one operational `EventOccurred` diagnostic
before runtime-observer fan-out. The record carries endpoint identity,
instrument identity, and Event path.

An Event occurrence is instantaneous, so it has no operation identifier,
duration, or outcome. Attachment generation is not recorded because it belongs
to the northbound projection rather than the UI- and northbound-neutral runtime
Event model.

## Privacy boundary

The Event value or payload is never a diagnostic field. This includes
`ByteArray` contents, exception messages, stack traces, protocol payloads, and
transport bytes.

## Fan-out boundary

Diagnostics are published once at the runtime occurrence boundary, not once per
runtime observer or northbound subscription. Diagnostic sink failure remains
isolated and cannot prevent delivery to Event observers.

## Noise boundary

Subscription creation, replay checks, observation buffering, formatting, and
UI delivery are not instrumented.

## Validation

Tests cover structural identity, payload privacy, absence of operation-only
fields, one record despite multiple observers, and diagnostic-sink isolation.
