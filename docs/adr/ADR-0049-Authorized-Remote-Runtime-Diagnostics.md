# ADR-0049 — Authorized Remote Runtime Diagnostics

- Status: Accepted; implemented, physically validated, and closed
- Date: 2026-08-07

## Context

ADR-0040 through ADR-0042 established bounded local Runtime Host and Client
diagnostics. ADR-0048 added exact bounded SCPI Protocol and Bytes observations
inside the serialized instrument session, but those southbound Runtime Host
records deliberately remained local. A remote Client requires an explicit
projection, authorization, disclosure ceiling, deployment procedure, and
independent recovery boundary. Network reachability and mutual-TLS
authentication alone must not grant diagnostic access.

## Decision

### Sanitized Host projection

The Runtime Host projects only the established normalized diagnostic fields.
Operational, Protocol, and Bytes disclosure levels are cumulative and bounded
by both the local capture ceiling and the configured remote ceiling. Bytes are
copied and bounded to 256 captured bytes while preserving original length and
truncation. Projection rejects prohibited detail keys and never exposes
deployment addresses, credentials, certificate material, serial assignments,
exception messages, requested values, or hidden payload reconstruction.

Each subscription owns a positive sequence beginning at one. Overflow,
retention loss, and a non-contiguous Client sequence are explicit gaps. A new
subscription begins a new sequence and does not replay records from an earlier
subscription.

### Authentication and authorization

The existing mutual-TLS boundary supplies the authenticated Client principal.
The `diagnostics.subscribe` permission is evaluated by the Runtime Host policy
for every diagnostic subscription. Missing identity, missing policy, unknown
principal, and absent permission fail closed. Authorization-policy content and
principal values are externally provisioned and are not generated or printed
by HASE deployment tooling.

### Deployment custody

Fresh installation keeps remote diagnostics disabled unless explicitly given
an existing policy source and remote maximum level. Existing installations use
an offline migration while the Host is stopped. Migration verifies and copies
the policy bytes, atomically replaces the application profile, retains the
original profile, and rolls back incomplete work. The guided updater hashes and
preserves the installed policy.

The supervised restore tool atomically reinstates the original profile,
retains the migrated profile, and retains the unchanged policy as inactive.
Post-replacement failure restores the migrated state. Neither migration nor
restore starts the Host or changes Client configuration.

### Client subscription and presentation

`Hase.Client` owns transport-independent immutable diagnostic models. The gRPC
adapter strictly maps the version 1 contract, rejects unspecified enums,
invalid identifiers, invalid timestamps, invalid byte bounds, overflow, and
sequence gaps, and deterministically disposes every subscription.

Each connected profile session owns an independent diagnostic subscription.
Recoverable diagnostic-stream failures use fresh subscriptions under the
bounded immediate, one-, two-, five-, and ten-second schedule. Authentication,
authorization, invalid-request, and unsupported-service failures are not
retried. No record is replayed. Failure presentation uses fixed sanitized
classifications and does not affect inventory, Property, Command, or Event
operation.

Remote records enter the existing bounded Client diagnostics collector with
the authoritative Host timestamp and exact profile/Host scope. The existing
window provides cumulative level, category, and Runtime Host filters,
pause/resume, retention, metadata, endpoint and generation fields, and exact
projected byte summary and hexadecimal content. Local Client activity and
projected Runtime Host activity remain distinguishable by profile context and
metadata.

## Safety and failure semantics

- Diagnostic observation is read-only and never invokes an endpoint operation.
- No Property write or Command is retried or replayed by diagnostics recovery.
- An uncertain mutation outcome remains uncertain.
- A failed diagnostic subscription does not disconnect an otherwise healthy
  Runtime Host Client session.
- Multi-host streams have independent subscription, sequence, recovery, and
  profile ownership.
- Client shutdown and profile disconnect cancel and dispose active diagnostic
  calls.

## Physical validation

Closure used a supervised Desktop Host and Laptop Client run with locally
provisioned policy values that were not disclosed:

1. Update the Runtime Host and Client applications while both are stopped.
2. Migrate the existing Runtime Host profile with a policy granting the
   authenticated Client `diagnostics.subscribe` permission and a selected
   remote ceiling.
3. Start Host and Client and confirm authorized Operational records appear in
   the Client Diagnostics window with the correct Runtime Host filter.
4. At Protocol and Bytes ceilings, perform one passive KEL-103 health exchange
   and one authoritative measurement read. Confirm correlated SCPI metadata,
   exact `0D` request and `0A` response bytes, endpoint scope, and bounded byte
   presentation.
5. Confirm a Client without the grant receives only sanitized authorization
   denial and no projected Host record.
6. Disconnect and reconnect the authorized Client and confirm a fresh stream,
   no replay, no duplicate record, and unaffected Host endpoint state.
7. Restore the pre-migration profile, restart the Host, and confirm remote
   diagnostics are disabled while normal inventory and operations remain
   available.

All checks passed. Authorized Operational, Protocol, and Bytes records reached
the existing Client collector with the Desktop Runtime Host profile and KEL-103
endpoint scope. One authoritative voltage read succeeded. Its exact request
ended in `0D`; the fragmented response was retained as exact receive chunks and
the final correlated chunk ended in `0A`.

USB removal was detected passively and reconnection returned the endpoint to
`Ready` through authoritative recovery. A Client reconnect created a fresh
diagnostic stream without replay or duplicate records. Substitution of an
externally prepared policy that removed only `diagnostics.subscribe` produced a
sanitized denial and disclosed no Host record while inventory and authoritative
Property reads remained available. The exact authorized policy was restored,
then the pre-migration profile was restored. With remote diagnostics disabled,
normal inventory, KEL-103 `Ready`, and authoritative reads remained available
without projected Host records or repeated diagnostic noise.

Validation was read-only, preserved mutation uncertainty and no-replay
semantics, and ended with the KEL-103 in authoritative CC/OFF state and the
external laboratory supply output OFF. ADR-0049 closes with 5,726 automated
tests passing in Visual Studio 2026 Release.
