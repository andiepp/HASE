# ADR-0035 Increment 5 — Explicit Parameterless Command Execution

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host operator console can now execute explicitly selected
parameterless Commands.

Each Command inventory snapshot carries:

- the complete normalized `RuntimeHostCommandTarget`;
- immutable descriptor path, display name, and description; and
- endpoint readiness.

The target contains endpoint identity, attachment generation, instrument
identity, and Command path. An execution captures that target once before
awaiting the operation. A later inventory refresh may replace the projected
target but cannot retarget the in-flight execution.

Unchanged Command descriptors retain their persistent ViewModel instances.
Inventory refresh updates only the mutable target and endpoint readiness.
Changed immutable descriptor metadata still replaces the ViewModel.

`MainWindowViewModel` coordinates parameterless execution through the existing
`IDesktopRuntimeHostOperator`. It:

- requires the Desktop Runtime Host to be running;
- requires the endpoint to be ready;
- prevents overlapping execution of the same Command;
- passes a null argument explicitly;
- invokes the normalized operator exactly once;
- authoritatively rereads readable Properties in the same captured endpoint
  generation and instrument after success;
- performs no automatic retry; and
- performs no optimistic Property mutation.

The persistent Command ViewModel projects:

- `Ready`;
- `Executing`;
- `Succeeded`;
- `Rejected`;
- `Failed`; and
- `Cancelled`.

Normalized stale-target, missing-target, unsupported-argument, and
endpoint-rejection outcomes are projected as rejected. Availability, endpoint
failure, timeout, and thrown exceptions are projected as failed. Cancellation
is projected separately.

An endpoint-provided return value is formatted invariantly and displayed after
success. The return value is never inferred to belong to a Property. Instead,
readable Properties in the executed instrument are refreshed once through the
normalized authoritative Property service. The runtime cache and subsequent
inventory refresh remain authoritative.

A failed post-Command Property read does not change the successful Command
outcome. It produces an explicit reconciliation warning and is not retried.

The Command card adds:

- `Execute Command`;
- current execution state;
- a concise result message; and
- an optional return value.

Automated tests cover:

- exact target capture;
- explicit null argument;
- successful return-value projection;
- normalized rejection;
- thrown failure;
- cancellation;
- single-flight protection;
- captured-target stability during attachment-generation replacement; and
- endpoint-readiness gating;
- successful post-Command authoritative Property reconciliation; and
- reconciliation warning projection without changing Command success.

This increment supports only the parameterless semantics of the current HASE
`CommandDescriptor`. It introduces no Command argument editor, automatic retry,
optimistic Property update, numeric or string Property write, activity log,
persistent audit history, runtime contract change, transport change, protocol
change, or gRPC change.
