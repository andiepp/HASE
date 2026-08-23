# ADR-0061 — Instrument and Media Examples

- Status: Accepted; Increment 61A decision acceptance
- Date: 2026-08-23
- Starting baseline: `8abf9c1da6abd019de7fbe62bdebfdf05f3f4564`
- Starting subject: `ADR-0060 closing`
- Starting complete Release baseline: 6,463 passed, 0 failed, 0 skipped

## Context

ADR-0060 closed with a five-example onboarding ladder covering HASE's
protocol-native endpoint families: simulation, Compact Serial, native
Protocol Version 1, the secured remote client, and the multi-host Client.
Two capability areas the framework already implements are not yet
represented in the public examples: SCPI laboratory instruments (ADR-0044
through ADR-0049) and runtime-hosted live media (ADR-0055 through
ADR-0057).

Two structural facts shape the extension:

1. The KORAD KEL-103 is already a fully supported instrument — its
   characterization, versioned definitions, runtime adapter, supervision,
   and hosting shipped under ADR-0044 through ADR-0049, and the endpoint
   composition schema and the development-profile backend both carry the
   `Kel103Serial` family. "Using the KEL-103" is therefore configuration,
   not development, and belongs in an example; "adding a new SCPI
   instrument" is C# authoring and belongs in an authoring guide, the
   sibling of the ESP32 Endpoint Authoring Guide.
2. The development loopback profile deliberately rejects Runtime Host
   media (ADR-0060, Increment 60C1), and media authorization is bound to
   client certificates. A webcam example therefore builds on the secured
   Example 3 setup. The existing media enablement tooling is partly
   machine-specific and needs the same generalization treatment the
   provisioning tooling received under ADR-0060.

## Decision

ADR-0061 extends the published onboarding material with three
deliverables, in this order:

### Example 5 — A laboratory instrument (KEL-103)

`docs/examples/Example-5-KEL-103.md`, on the certificate-free development
profile, configuration-only: the physical KEL-103 on USB serial, the
explicit `Kel103Serial` composition entry (SCPI endpoints are configured,
never discovered), the definition version selection, live measurement
Properties in the Client, and supervision and recovery behavior. The
primary walkthrough is read-only. Controlled operation — setpoint writes,
mode Commands, input activation, confirmed SHORT — is an advanced section
that explains the ADR-0046 safety model: authoritative input-OFF
interlocks, single transmission, authoritative readback, no retry and no
recovery replay. The exact definition identifier and the recommended
version for a new user are pinned by read-only inspection during
authoring.

### SCPI Instrument Authoring Guide

`docs/SCPI-Instrument-Authoring-Guide.md`, the "from scratch" story at its
true altitude: what it takes to bring a new SCPI instrument into HASE,
derived from the KEL-103 implementation as the worked reference. The guide
walks the layered boundary — read-only characterization first, under the
ADR-0044 discipline of one serialized pipeline, bounded responses, and no
mutation retry; then the versioned instrument definition; the runtime
adapter mapping normalized Properties and Commands to device queries;
hosting and attachment integration; and the composition entry. This
objective writes the guide from the existing sources; authoring a new
physical instrument end-to-end remains explicitly deferred future work.

### Example 6 — A webcam (live video)

`docs/examples/Example-6-Webcam.md`, on the secured Example 3 setup: a
camera on the host PC, media authorization for the enrolled client
principal, the media configuration and binding, and the Client's detached
video window with explicit Start and Stop and the source-loss behavior.
The ADR-0055 contracts are stated plainly in the document: view-only, one
session and one viewer, explicit Client-controlled start, no recording, no
public relay, and no device-identifier disclosure.

Because the existing media binding and enablement scripts are bound to
project machines by name, a preceding increment generalizes the minimal
enablement path a new user needs — as a neutral recipe, a neutral script,
or a setup-wizard extension, decided by read-only inspection of what the
binding, grant, and configuration steps actually require. Cryptographic
and custody behavior of the validated tooling is reused, not reinvented.

## Constraints

- The SCPI safety boundary is unchanged: no arbitrary operator SCPI
  console, characterization-first authoring, input-OFF interlocks, one
  transmission per mutation, no retry, no recovery replay.
- The media contracts are unchanged: the development profile continues to
  reject media; one session, one viewer, no recording, no public relay;
  media grants remain explicit per principal.
- The setup wizard keeps all cryptography with the validated ADR-0032
  provisioning scripts.
- Examples remain configuration-and-operation documents; authoring content
  lives in guides. Distribution remains clone-and-build.

## Consequences

### Positive

- The public material covers every endpoint family HASE implements,
  including the SCPI adapter boundary and live media.
- The authoring guide gives the "add your own instrument" answer at the
  correct altitude instead of disguising configuration as authoring.
- The media generalization removes the last machine-specific tooling from
  a user-facing path.

### Negative

- Example 5's walkthrough requires the physical KEL-103; readers without
  one read it as a pattern demonstration.
- Example 6 inherits the full secured setup as a prerequisite and touches
  real camera custody on the host machine during walkthroughs.

### Neutral

- Runtime, protocol, adapter, and northbound contracts are unchanged;
  ADR-0061 adds documentation and, at most, neutral tooling around the
  validated media enablement path.

## Increment plan

Each increment is separately approved. Later increments are refined when
reached.

### Increment 61A — Decision acceptance

Exact repository scope: this ADR, `docs/ProjectStatus.md`, and
`docs/Roadmap.md`. Documentation-only; validation is consistency, exact
Git scope, `git diff --check`, and diff inspection. Physical effects:
none. Rollback: revert before commit.

### Increment 61B — Example 5 document

Read-only inspection (definition identifier and versions, serial
parameters, development-profile KEL-103 path) and the published document
with links into the ladder.

Completed result: commit `f84dc087a60c8e7f551b4c1589f18190cfff2ea7`
publishes `docs/examples/Example-5-KEL-103.md`. Inspection pinned the
grounded values: one descriptor identifier `kel103-identity` with
composition-accepted versions 2 through 5, version 3 recommended as the
complete read-only first contact, version 5 for controlled operation, the
characterized 115200-baud serial parameters, and the user-chosen logical
endpoint identity. The document states the configured-never-discovered
and no-SCPI-console boundaries up front and teaches the ADR-0046 safety
model before the controlled section.

### Increment 61C — Example 5 walkthrough and closure

Operator walkthrough with the physical KEL-103 on the development
profile; corrective sub-increments as findings arise; documentation-only
closure.

Completed result: the operator performed the walkthrough on AEPRAKETE
with the physical KEL-103. One finding: after the secured Examples 3 and
4, the instruction "start exactly as in Example 0" was ambiguous between
the development and secured worlds, and the operator reached for the
secured host. Corrective increment 61C1, commit
`72edc97ee54ea480866f5081a83ef4c9f509e9dc`, replaced it with the explicit
development-pair launch blocks and the notes that a secured host on the
same machine must be closed first and that the development host is
invisible to remote Clients by design. The operator then validated the
complete example: identity verification to `Ready`, live measurements,
and controlled operation under the input-OFF interlocks, accepting both
the read-only and controlled paths as working.

### Increment 61D — SCPI Instrument Authoring Guide

The guide derived from the KEL-103 sources, linked from the README and
Example 5.

### Increment 61E — Media enablement generalization

Read-only inspection of the binding, grant, and configuration steps a new
user needs; then the neutral recipe, script, or wizard extension that
inspection justifies — with focused automated tests if tooling changes.

### Increment 61F — Example 6 document

The published webcam example on the secured setup.

### Increment 61G — Example 6 walkthrough and closure

Operator walkthrough with a camera on the host PC; corrective
sub-increments; documentation-only closure.

### Increment 61H — Objective closure

Reconcile this ADR, Project Status, and Roadmap. Documentation-only.

## Deferred scope

- Authoring a new physical SCPI instrument end-to-end (a future objective
  with real hardware);
- generic VISA, USBTMC, and GPIB support and automatic instrument
  discovery;
- a public instrument-definition repository;
- media extensions: PTZ, recording, snapshots, multiple viewers, public
  relay, dynamic microphone discovery, non-Windows capture;
- everything deferred by ADR-0060.
