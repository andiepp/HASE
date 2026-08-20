# ADR-0058 — Operator-Initiated Runtime Endpoint Refresh

- Status: Closed; Increment 58D documentation closure
- Date: 2026-08-20
- Starting baseline: `f7615ae79e72efc48935eed63e02ff650d2d0a87`
- Starting subject: `Close ADR-0057 client workspace redesign`
- Starting complete Release baseline: 6,369 passed, 0 failed, 0 skipped

## Context

The Windows Runtime Host reads its endpoint-composition profile during startup
and attempts to attach every configured physical endpoint. A successfully
attached endpoint is published through the Runtime Host inventory. The
attachment then owns its established disconnect, reconnect, resynchronization,
generation, Property, Command, Event, and diagnostic lifecycles.

Startup deliberately tolerates a configured physical endpoint that is
unavailable. The Runtime Host remains operational, publishes the endpoints that
were attached successfully, and records a scoped `EndpointStartupUnavailable`
diagnostic. An endpoint that was not attached during startup never enters the
attachment inventory, however, so its existing reconnect supervisor cannot
observe it when the device becomes available later.

The Runtime Host WPF application currently refreshes its presentation from the
existing attachment inventory once per second. This read-only projection
refresh updates state displayed for published endpoints but does not enumerate,
verify, or attach a configured endpoint that is absent from that inventory.
Restarting the Runtime Host is therefore the only current way to retry such an
endpoint.

HASE already separates transport candidate metadata from authoritative endpoint
identity. Network and USB serial discovery cannot assign identity, and a
visible network service, address, serial port, USB adapter, or SCPI port is not
proof of the configured HASE endpoint. Existing attachments must never be
replaced merely because a later candidate is visible.

## Decision

The Windows Runtime Host main window adds a `Refresh` button immediately
adjacent to `Open Diagnostics`. `Refresh` is an explicit operator request to
search for configured physical endpoints that are not currently published.

The operation is distinct from the existing one-second presentation refresh:

- the timer continues to reproject only the current attachment inventory;
- the operator command performs the bounded endpoint search and authoritative
  verification needed before a new attachment can be published; and
- completion triggers an immediate inventory and diagnostics projection
  refresh instead of waiting for the next timer tick.

### Eligible endpoint scope

One refresh operation considers only physical endpoints already declared in
the loaded Runtime Host endpoint-composition profile and not represented in the
current published attachment inventory.

The first implementation covers:

- configured native-network endpoints;
- configured Compact Serial endpoints, including Windows USB serial candidate
  enumeration and Compact bootstrap verification; and
- configured KEL-103 serial endpoints.

The optional in-process byte-buffer simulation is excluded because it is not a
physical discovery target and is deterministically attached during startup.

An endpoint that is absent from the endpoint-composition profile is outside
ADR-0058. Discovering and presenting arbitrary unconfigured candidates for
operator selection would require a separate inventory, selection, persistence,
and composition-editing decision.

### Identity and attachment rules

Refresh preserves the existing HASE authority boundaries:

1. candidate metadata locates a possible connection only;
2. the existing protocol- or adapter-specific verification path establishes
   authoritative identity;
3. the authoritative identity must exactly equal the configured expected
   endpoint identity; and
4. only that exact configured endpoint may be attached.

An existing published attachment is never refreshed, detached, replaced, or
re-created by this command, regardless of whether its state is `Ready`,
`Recovering`, `Disconnected`, or `Faulted`. Its existing recovery supervisor
remains authoritative.

Pressing `Refresh` therefore does not change an existing attachment generation,
restart a connection, discard cached values, replay an Event, retry a Property
write or Command, or start, stop, resume, or switch media.

### Concurrency and lifecycle

At most one operator refresh may run at a time. The command is enabled only
while the Runtime Host is running and no refresh is already active. The button
is disabled for the duration of the operation.

The backend rechecks eligibility before each attachment attempt so a candidate
cannot be attached twice if inventory state changes during the operation.
Refresh cancellation participates in Runtime Host shutdown. A failed or
unavailable candidate is isolated from other eligible configured endpoints.

### Diagnostics and presentation

The Runtime Host publishes sanitized operational diagnostics for the refresh
lifecycle and per-endpoint outcome. Diagnostic records may identify the
configured endpoint, endpoint family, and bounded failure classification, but
must not disclose protected local configuration or device identity beyond the
existing diagnostic policy.

The main-window endpoint list remains a projection of the authoritative
attachment inventory. It is not independently edited by the Refresh command.
A successfully attached endpoint appears when that inventory is reprojected.

## Consequences

### Positive

- A configured endpoint connected after Runtime Host startup can be added
  without restarting the process.
- The operation is explicit, bounded, observable, and compatible with existing
  startup attachment behavior.
- Existing endpoint recovery, generation, cache, operation, and Event semantics
  remain unchanged.
- Authoritative verification prevents USB, network, or serial metadata from
  silently assigning HASE identity.
- Independent candidate failures do not make the running Runtime Host faulted.

### Negative

- The operator must press `Refresh`; ADR-0058 does not add continuous physical
  endpoint hot-plug monitoring.
- Refresh may take the configured verification timeout for each unavailable
  candidate because existing discovery remains sequential.
- An unconfigured endpoint remains unavailable until the composition profile is
  changed through a separately approved workflow and the application is
  restarted or a future composition-management boundary is implemented.

### Neutral

- Existing published endpoints continue to detect their own disconnect and
  reconnect automatically.
- The Runtime Host endpoint-composition file remains the allowlist of physical
  endpoints eligible for publication.
- Northbound contracts and the Client require no ADR-0058 protocol change; an
  attached endpoint is exposed through the existing snapshot and observation
  boundaries.

## Increment plan

### Increment 58A — Decision acceptance

Goal: record the approved architecture, implementation stages, invariants, and
validation boundary.

Exact repository scope:

- `docs/adr/ADR-0058-Operator-Initiated-Runtime-Endpoint-Refresh.md`;
- `docs/ProjectStatus.md`; and
- `docs/Roadmap.md`.

Automated validation is limited to documentation consistency, exact Git scope,
`git diff --check`, and final diff inspection. No .NET build or test is required
because 58A changes no executable or project file.

Physical and deployment effects: none. Increment 58A does not start an
application, enumerate or verify a device, attach or detach an endpoint, access
a serial port or network endpoint, deploy software, or change configuration,
credentials, firmware, firewall, privacy, media, or physical state.

Rollback boundary: remove the new ADR and revert only the two synchronized
documentation edits before commit. No runtime or external recovery is required.

Definition of done: the three documents consistently mark ADR-0058 accepted,
retain the exact starting baseline, and define 58B as the next separately
approved repository increment.

### Increment 58B — Refresh boundary and Runtime Host UI

Goal: implement the serialized backend operation and the adjacent WPF Refresh
button without deploying or exercising physical endpoints.

Expected implementation scope includes:

- a narrow Runtime Host endpoint-refresh contract;
- reuse or extraction of the existing authoritative startup attachment paths;
- configured-but-unpublished eligibility and duplicate prevention;
- isolated per-endpoint outcomes and sanitized diagnostics;
- shutdown-aware cancellation and single-operation serialization;
- an asynchronous WPF command with running/busy eligibility;
- the adjacent `Refresh` and `Open Diagnostics` controls; and
- focused backend, view-model, and presentation contract tests.

Validation runs the smallest relevant focused Release suites first and then the
complete `HASE.slnx` Release suite. Repository application, validation, commit,
push, three-computer synchronization, deployment, and physical validation
remain separate stop points.

Completed result: Increment 58B changed exactly 13 source and test paths. The
implementation is commit `fa491eeb821bcf0252ff71542d89605377187ed8`, subject
`Add operator-initiated runtime endpoint refresh`, with parent documentation
commit `039f28aad45dde425e8b887bc41e5c6d41d458dc`. Focused validation and the
complete Release suite succeeded on AEPRAKETE. The complete result is 6,379
passed, zero failed, and zero skipped; the successful complete build reported
60 warnings. AEPRAKETE, LABC, and LTAEP are clean and synchronized at the
implementation commit.

### Increment 58C — Controlled deployment and physical validation

Goal: prove the accepted behavior on AEPRAKETE after separately approved source
validation, commit, push, and required repository synchronization.

The controlled scenario starts the Runtime Host with one configured endpoint
physically unavailable, proves that the endpoint is initially absent, makes it
available, presses `Refresh`, and proves that exactly one new attachment becomes
published and `Ready`. It then repeats Refresh to prove duplicate prevention and
checks that pre-existing endpoint identities, generations, and operation remain
unchanged.

Unavailable and mismatched-identity outcomes must remain isolated and
diagnostic. The exact physical family or families used for validation will be
selected from the configured AEPRAKETE inventory during a read-only preflight;
58A does not authorize that preflight or any physical action.

Completed result: the read-only preflight selected configured Compact Serial
endpoint `arduino-uno-01` from a three-endpoint composition containing one
native-network, one Compact Serial, and one KEL-103 serial endpoint. The
application-only AEPRAKETE deployment completed from the exact implementation
commit with six warnings and preserved configuration, identity, authorization,
shortcut, and WebView2 custody. The installed executable SHA-256 is
`00B35BEABFD5C903A4CB5435614786F6717CDBAC80D5605B98274E13324FF74A`.

Physical validation started the Runtime Host with the Arduino unavailable. The
ESP32 and KEL-103 remained published and `Ready`, and the unavailable Arduino
was isolated diagnostically. Connecting the Arduino after startup and pressing
Refresh attached it exactly once and brought it to `Ready` without restarting
the Runtime Host. A second Refresh produced no duplicate and changed none of
the three attachment generations. The operator accepted the complete endpoint,
generation, diagnostic, and shutdown checklist as working perfectly.

A byte-verified 615-file, 181,121,200-byte pre-deployment application copy
remains in local rollback custody at
`H:\HASE-Packages\HASE-ADR-0058-58C2-Rollback-AEPRAKETE`. Its manifest
SHA-256 is
`DF1289581DB888BFD4E71DD2B209B4C959A486CE9F0CCD664A8F07A909AE3CB2`.
Closure does not authorize cleanup.

### Closure

Increment 58D reconciles this ADR, Project Status, and Roadmap with the exact
implementation commit, 6,379-test automated baseline, clean three-computer
synchronization, controlled deployment, retained rollback evidence, and
operator-accepted physical result. It is documentation-only and performs no
build, test, deployment, application start, device access, endpoint attachment,
configuration change, firmware action, media operation, cleanup, or physical
mutation.

ADR-0058 is closed.

## Deferred scope

- automatic attachment without an explicit operator request;
- automatic replacement of a published attachment;
- continuous Added/Updated/Removed endpoint presence monitoring;
- arbitrary unconfigured-candidate presentation and selection;
- runtime editing or persistence of endpoint composition;
- parallel candidate verification;
- Linux USB serial discovery;
- IPv6 discovery or cross-subnet mDNS relaying;
- generic automatic SCPI/VISA/USBTMC/GPIB discovery; and
- remote endpoint lifecycle administration from the Client.
