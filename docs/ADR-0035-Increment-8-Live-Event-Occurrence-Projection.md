# ADR-0035 Increment 8 — Live Event Occurrence Projection

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host operator console now subscribes once to its existing
normalized runtime-host observation service and projects endpoint-originated
Event occurrences.

Each immutable occurrence captures directly from one normalized observation:

- endpoint occurrence time in UTC;
- endpoint identity;
- attachment generation;
- instrument identity;
- Event path; and
- invariantly formatted optional value.

The source endpoint and attachment generation come from the same observation
as the Event payload. They are never inferred from current UI selection,
descriptor lookup state, or the preceding occurrence.

The separate `Endpoint Events` history:

- contains only `EventOccurred` observations;
- retains at most 100 occurrences;
- inserts the newest occurrence first;
- remains in memory for the process lifetime; and
- does not add entries to the operator activity log.

The WPF subscription starts after the production runtime host starts. It is
cancelled and awaited before runtime shutdown. A terminated read-only
subscription cannot terminate the WPF dispatcher or prevent orderly host
shutdown.

## Verification

Automated coverage includes:

- newest-first ordering;
- the 100-occurrence retention boundary;
- null rejection;
- exact source and payload projection;
- consecutive Arduino and ESP32 occurrences retaining independent sources; and
- rejection of non-Event observations.

This increment introduces no replay, acknowledgement, persistence, filtering,
export, automatic resubscription, protocol change, runtime contract change, or
gRPC change.
