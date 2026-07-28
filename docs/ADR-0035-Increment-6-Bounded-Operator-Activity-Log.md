# ADR-0035 Increment 6 — Bounded Operator Activity Log

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host operator console now projects a process-local record
of completed local operator actions.

Each immutable activity entry captures:

- UTC completion timestamp;
- action kind;
- endpoint identity;
- captured attachment generation;
- instrument identity;
- Property or Command path;
- requested value or argument summary;
- normalized outcome;
- concise diagnostic; and
- post-Command Property reconciliation summary.

The console records Boolean Property writes and parameterless Command
executions after they cross the normalized operator boundary. A Command and its
post-Command authoritative Property reconciliation produce exactly one entry.
Returned rejection, returned failure, thrown failure, and cancellation are
recorded with their normalized outcomes.

Actions rejected locally before execution are not activity. In particular, an
action blocked because the runtime is stopped, the endpoint is not ready, the
operation is already executing, or the requested Boolean value is unavailable
does not create an entry.

The activity collection:

- is retained only for the lifetime of the Desktop Runtime Host process;
- contains at most 100 entries;
- inserts the newest completed entry first; and
- discards the oldest entry when capacity is exceeded.

The WPF shell displays the collection in a read-only `Operator Activity`
section. The projection does not include runtime-host binding addresses,
configuration paths, stack traces, health probes, ordinary inventory refreshes,
or operations performed independently by a Laptop Client.

## Verification

Automated coverage includes:

- immutable field and UTC timestamp capture;
- newest-first ordering;
- the 100-entry retention boundary;
- exact write target and requested-value activity projection;
- exact parameterless Command target and argument projection;
- successful reconciliation summary projection; and
- absence of activity for locally blocked writes and Commands.

This increment introduces no persistent audit storage, export, filtering,
remote activity aggregation, runtime contract change, transport change,
protocol change, or gRPC change.
