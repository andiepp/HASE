# ADR-0035 — Interactive Operator Console

## Status

Accepted

## Context

ADR-0034 established the Windows Desktop Runtime Host as the production owner of
physical ESP32 and Arduino Uno connections. The application starts and stops the
runtime orderly, exposes the authenticated northbound API, and projects a
persistent descriptor-driven runtime inventory with live read-only Property
updates.

The Desktop Runtime Host is currently observational. Routine operator work still
requires a separate client even though the host process already composes the
normalized Property and Command application services.

The Desktop Runtime Host should support explicit operator mutations without
allowing WPF, ViewModels, transports, or endpoint-family-specific code to bypass
the established runtime-host service boundary.

## Decision

The Desktop Runtime Host will become an interactive operator console.

Operator actions use the same normalized runtime-host Property and Command
semantics as northbound clients. The Desktop Runtime Host remains the exclusive
owner of physical endpoint connections.

The implementation will provide:

- descriptor-driven writable Property projection;
- operator-requested values kept separate from authoritative current values;
- explicit Property write execution;
- persistent descriptor-driven Command projection;
- explicit Command execution;
- descriptor-driven Command argument entry where supported;
- visible operation lifecycle and outcome;
- attachment-generation-aware targeting;
- single-flight protection for each projected operation;
- endpoint-confirmed authoritative state; and
- a bounded in-memory operator activity log using UTC timestamps.

## Application boundary

Operator mutations execute through a UI-independent application service in
`Hase.DesktopHost`.

The service delegates to the existing normalized:

- `IRuntimeHostPropertyService`; and
- `IRuntimeHostCommandService`.

It does not call endpoint attachments, transports, protocol connections, or
endpoint-family-specific implementations directly.

Every target includes:

- authoritative endpoint identity;
- current attachment generation;
- instrument identity; and
- Property identity or Command path.

The service preserves normalized operation results without translating
diagnostics into application logic.

## Mutation semantics

Every Property write and Command execution is explicit and single-shot.

The operator console:

- never automatically retries a mutation;
- never optimistically changes authoritative Property state;
- propagates cancellation;
- reports returned failures and thrown failures;
- rejects stale attachment generations through the existing normalized service
  contracts; and
- accepts only endpoint-confirmed values or normal observation updates as
  authoritative.

A changing live Property value must not overwrite a pending operator-requested
value.

## Availability rules

An operator action is unavailable when:

- the runtime host is not running;
- the endpoint is not ready;
- the projected attachment generation is no longer current;
- the descriptor does not support the requested operation;
- required operator input is missing or invalid;
- the same projected operation is already executing; or
- the host is stopping.

Disconnecting during an operation may produce a failed or indeterminate outcome.
The console must expose that outcome rather than retrying automatically.

## Concurrent clients

The Desktop Runtime Host may serve local and remote clients while an operator
uses the host console.

Local clients continue to use the authenticated loopback northbound endpoint.
Remote clients continue to use the explicitly configured private-network
endpoint. Mutual TLS remains required. All physical endpoint ownership stays
inside the Desktop Runtime Host.

## Consequences

### Positive

- Local and remote mutations retain one normalized semantic model.
- WPF remains outside runtime, transport, and protocol projects.
- Attachment replacement cannot silently retarget an operator action.
- Operator input remains stable while authoritative values continue updating.
- Physical endpoint results remain authoritative.
- Operation behavior can be tested without WPF or physical hardware.

### Negative

- The presentation must maintain separate requested and authoritative values.
- Operation lifecycle adds mutable state to otherwise persistent projections.
- Descriptor-driven editors require explicit support for each value shape.
- Disconnects can leave an operation outcome uncertain and must be communicated
  clearly.

## Excluded work

ADR-0035 does not introduce:

- endpoint discovery or attachment controls;
- transport-level connect or disconnect controls;
- configuration editing;
- certificate provisioning or export;
- automatic mutation retry;
- scheduled or scripted operations;
- persistent audit history;
- Windows service or system-tray behavior;
- remote-host administration; or
- northbound gRPC contract changes unless a concrete capability gap is proven.

## Implementation sequence

1. UI-independent operator-operation foundation.
2. Persistent Command projection.
3. Independent writable Property input state.
4. Property write execution and presentation.
5. Command execution and presentation.
6. Descriptor-driven Command arguments.
7. Bounded operator activity projection.
8. Physical validation and documentation.

