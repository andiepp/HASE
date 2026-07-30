# ADR-0040 Increment 40F1 — Desktop Diagnostic Session Foundation

## Scope

Establish the local Desktop Runtime Host diagnostic-session boundary without
adding diagnostic presentation controls or WPF layout.

## Session ownership

`DesktopRuntimeDiagnosticSession` owns one
`BoundedRuntimeDiagnosticCollector` and its matching
`RuntimeDiagnosticPublisher`. The session retains at most 2,000 records and
exposes ordered snapshot and clear operations through
`IDesktopRuntimeDiagnosticSource`.

The production backend creates a new session before constructing its runtime
attachment host. Stop, failed startup, and subsequent restart do not reuse the
previous collector.

## Local level configuration

The Desktop Runtime Host defaults to `Operational`. A local startup may select
one cumulative maximum level:

- `--diagnostics=operational`;
- `--diagnostics=protocol`; or
- `--diagnostics=bytes`.

The byte level therefore remains disabled unless it is explicitly selected
locally before runtime startup. The existing optional simulation switch may be
combined with one diagnostic-level switch.

## Shared publisher

`RuntimeEndpointAttachmentHost` production factories accept an optional
diagnostic publisher and pass it into their shared `RuntimeContext`. Existing
callers that omit the publisher retain the existing null-sink behavior.

The Desktop backend injects its session publisher before any endpoint is
attached and passes the runtime context's same publisher into northbound
composition. Runtime lifecycle, interaction, Protocol, and byte producers
therefore share one process-local sequence and bounded collector.

## Boundary

The source is registered only in the local Desktop application composition.
No northbound service, gRPC contract, remote level control, persistence, or
export is added. Snapshot capture and clearing are observational and do not
alter runtime authority, endpoint behavior, logging, or transport statistics.

## Verification

Focused tests cover the Operational default, cumulative Protocol and Bytes
enablement, bounded ordered retention, clearing, independent replacement
sessions, duplicate startup-level rejection, and attachment-host publisher
identity.

WPF projection and presentation remain deferred to Increment 40F2 and later.
