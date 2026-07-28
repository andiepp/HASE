# ADR-0035 Increment 1 — Operator Operation Foundation

## Status

Implemented for validation.

## Scope

This increment introduces a UI-independent Desktop Runtime Host operator
boundary.

`DesktopRuntimeHostOperator` delegates generation-scoped Property writes and
Command executions to the existing normalized runtime-host application services:

- `IRuntimeHostPropertyService`;
- `IRuntimeHostCommandService`;
- `RuntimeHostPropertyTarget`; and
- `RuntimeHostCommandTarget`.

The operator boundary preserves the target, requested value or argument,
cancellation token, returned normalized result, and thrown exception. It invokes
the corresponding runtime-host mutation exactly once and provides no automatic
retry.

Automated tests cover:

- required service dependencies;
- null mutation targets;
- exact Property-write delegation;
- exact Command-execution delegation;
- normalized returned failures;
- cancellation-token forwarding;
- thrown failure propagation; and
- single invocation when an operation fails.

No WPF view, ViewModel, runtime composition, endpoint attachment, transport,
protocol, gRPC contract, Property editor, Command projection, or physical
endpoint behavior is changed.

