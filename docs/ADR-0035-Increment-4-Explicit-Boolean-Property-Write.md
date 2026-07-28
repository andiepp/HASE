# ADR-0035 Increment 4 — Explicit Boolean Property Write

## Status

Implemented for validation.

## Scope

The Desktop Runtime Host operator console can now execute an explicit write for
a writable Boolean Property.

Each Property inventory snapshot carries:

- the complete normalized `RuntimeHostPropertyTarget`;
- descriptor-derived Boolean and write capability;
- authoritative current Boolean state; and
- endpoint readiness.

The target contains endpoint identity, attachment generation, instrument
identity, and Property identity. A write captures that target and the requested
Boolean value once before awaiting the operation. A later inventory refresh may
replace the projected target, but it cannot retarget the in-flight write.

The production backend implements `IDesktopRuntimeHostOperator`. While the
runtime is active, it delegates to one `DesktopRuntimeHostOperator` composed from
the existing normalized Property and Command services. The operator becomes
unavailable before runtime resources are disposed.

`MainWindowViewModel` coordinates Boolean writes and invokes the operator
boundary exactly once. A write cannot begin unless:

- the Desktop Runtime Host is running;
- the endpoint is ready;
- the Property is writable and Boolean;
- a requested Boolean value exists; and
- no write is already executing for that Property.

The persistent Property ViewModel projects:

- `Ready`;
- `Executing`;
- `Succeeded`;
- `Rejected`;
- `Failed`; and
- `Cancelled`.

Normalized stale-target, missing-target, unsupported, invalid-value, and
endpoint-rejection outcomes are projected as rejected. Availability, endpoint
failure, timeout, and thrown exceptions are projected as failed. Cancellation
is projected separately.

The Property card adds:

- `Write requested value`;
- current write state; and
- a concise result message.

Requested input is preserved after every result. A successful operation does not
optimistically modify authoritative current state. The endpoint-confirmed runtime
cache and subsequent inventory refresh remain authoritative.

Automated tests cover:

- exact target and requested-value capture;
- no optimistic authoritative update;
- requested-value preservation;
- normalized rejection;
- thrown failure;
- cancellation;
- single-flight protection;
- captured-target stability during attachment-generation replacement; and
- endpoint-readiness gating.

No automatic retry, numeric write, string write, Command execution, activity
log, persistent audit history, runtime contract change, transport change,
protocol change, or gRPC change is introduced.

