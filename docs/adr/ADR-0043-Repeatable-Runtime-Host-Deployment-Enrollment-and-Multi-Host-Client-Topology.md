# ADR-0043 — Repeatable Runtime-Host Deployment, Enrollment, and Multi-Host Client Topology

- Status: Accepted
- Date: 2026-08-01

## Context

ADR-0032 established a controlled private-network deployment and credential-
provisioning path. ADR-0033 through ADR-0042 then delivered reusable client
contracts, the WPF client and Desktop Runtime Host applications, descriptor-
driven operations and events, and bounded diagnostics on both sides of the
northbound boundary.

The physically validated topology still depends on development-oriented,
machine-specific startup data. The Desktop Runtime Host receives an external
private-network configuration path, an ESP32 address, optional simulation, and
a diagnostic level as command-line arguments. The WPF client receives exactly
one external private-network client configuration path and owns exactly one
runtime-host session.

The current production backend also supplies one fixed fallback
`RuntimeHostId`. That was sufficient for the validated single-host topology but
cannot identify several independently installed Runtime Hosts. Copying an
installation must not silently duplicate its authoritative identity.

HASE therefore needs a repeatable Release deployment and enrollment model
before it adds further host capabilities. The model must support several
Runtime Hosts and clients without weakening mutual TLS, certificate pinning,
explicit endpoint attachment, or runtime-host ownership of physical endpoint
lifecycles.

## Decision

### Scope and invariant boundaries

ADR-0043 will define:

- repeatable Release publication and startup for the Desktop Runtime Host and
  WPF client;
- installation-safe, stable Runtime Host identity;
- external application profiles for host composition and client host
  registries;
- explicit client enrollment and expected-host verification;
- several independent Runtime Host sessions in one client application; and
- repeatable Runtime Host, client, and endpoint onboarding procedures.

The Runtime Host continues to own physical endpoint discovery, verification,
attachment, supervision, recovery, detachment, and shutdown. A client profile
does not grant lifecycle administration. Tailscale supplies reachability only;
it is not HASE identity, authentication, or authorization.

### Two external configuration layers

Existing security-tested private-network documents remain the lower-level
security configuration:

- `desktop-private-network.json` defines the listener binding, server-
  certificate reference, and client-enrollment reference;
- `laptop-private-network.json` defines one HTTPS address, client-certificate
  reference, and pinned server-certificate reference; and
- `client-enrollments.json` defines enrolled client authorization.

Their bounded loading, strict field handling, certificate-store references,
and fail-closed behavior remain intact. Application profiles reference these
documents; they do not duplicate their certificate or enrollment contents.

The Desktop Runtime Host application profile is named conceptually
`desktop-runtime-host.json`. Version 1 owns:

- a format version;
- the fully qualified installation-identity file path;
- the fully qualified private-network deployment configuration path;
- the startup diagnostic level;
- optional validation simulation; and
- the explicitly configured endpoint-composition definitions introduced by a
  later ADR-0043 increment.

The client host registry is named conceptually `hase-client-hosts.json`.
Version 1 contains an ordered collection of profiles. Each profile owns:

- a stable client-local profile identifier;
- an operator-facing display name;
- the expected authoritative `RuntimeHostId`;
- the fully qualified private-network client configuration path; and
- an enabled or disabled state.

Application-profile files and every referenced deployment file remain external
to the repository. They must not expose credentials, private keys, passwords,
or deployment values through ordinary UI output, diagnostics, or documentation.

### Installation-safe Runtime Host identity

Every Runtime Host installation resolves one stable logical `RuntimeHostId`
according to ADR-0024 precedence:

1. an explicitly configured identity, when deliberately provided;
2. a previously persisted installation identity; or
3. a newly generated identity persisted before northbound publication.

The identity is independent of address, DNS name, machine name, Tailscale node,
endpoint inventory, process restart, and application upgrade. Copying program
files does not copy installation identity. Copying a complete installation data
directory is an explicit administrative action and duplicate active identity
must be surfaced as a conflict rather than accepted silently.

### Expected-host verification

Certificate authentication and exact server-certificate pinning remain
mandatory. After an authenticated connection yields its authoritative initial
snapshot, the client compares the received `RuntimeHostId` with the profile's
expected identity.

Mismatch fails that profile connection closed. It must not retarget the profile,
merge state, enable operations, or silently update the expected identity.
Changing an expected identity is a separate explicit enrollment action.

The client-local profile identifier and display name are not host identity and
are not authorization inputs.

### Multi-host ownership

One existing single-host session controller continues to own one
`IRuntimeHostClientSession`, cancellation source, observation task, sequence,
state model, operations, and diagnostics context.

A new UI-independent coordinator introduced by a later increment owns several
single-host controllers. Failure, cancellation, reconnection, sequence state,
or disposal for one host must not affect another host.

Endpoint operations are qualified by:

```text
RuntimeHostId + EndpointId + AttachmentGeneration
```

`EndpointId` is not assumed globally unique across Runtime Hosts. Profile order
is presentation order only. Loading profiles does not automatically connect,
and connecting does not automatically attach or replace endpoints.

### Startup and Release operation

Published applications receive exactly one application-profile path at
startup. The same argument works from PowerShell and a Windows desktop shortcut.
`launchSettings.json` remains development tooling and must not contain real
user-specific paths, private addresses, or credentials.

Machine-specific endpoint candidates move out of positional development
arguments and into the external Desktop Runtime Host application profile.
Ordinary startup uses the configured profile directly and must not unexpectedly
open a configuration-file picker.

Where practical, invalid startup configuration leaves the WPF shell open with
a truthful faulted status and a safe failure summary. It never falls back to
cleartext, wildcard binding, unauthenticated operation, a different host, or an
automatically selected endpoint.

## Consequences

- HASE can support several independently identified Runtime Hosts without
  conflating address, certificate, profile, host, endpoint, or generation
  identity.
- Existing private-network security documents and parsers remain reusable.
- Release startup no longer depends on Visual Studio or machine-specific
  `launchSettings.json` values.
- One unavailable or faulted Runtime Host does not invalidate other client
  sessions.
- Adding a host, client, or endpoint becomes a documented and testable
  administrative procedure.
- Configuration and enrollment become explicit deployment artifacts that must
  remain outside source control.
- Multi-host support increases lifecycle and presentation complexity, but that
  complexity is isolated above the existing single-host session controller.

## Planned increments

1. 43A1 — ADR-0043 Architecture and Configuration Boundaries.
2. 43A2 — Deployment and Multi-Host Configuration Contracts.
3. 43B — Release Publication and Runtime Host Launcher.
4. 43C — Client Release Publication and Launcher.
5. 43D — Multi-Host Client Session Core.
6. 43E — Multi-Host WPF Presentation.
7. 43F — Client Enrollment Recipe.
8. 43G — Runtime Host and Endpoint Onboarding Recipe.
9. 43H — Combined Multi-Host Validation and Closure.

## Validation state

- ADR-0043 starts from authoritative commit
  `c79d956de4603412c431425a94a7dac17ffae98d`.
- The accepted baseline has 4,017 automated tests passing.
- Increment 43A1 changes documentation only.
- No Release launcher, application profile, identity migration, multi-host
  session, enrollment change, or endpoint onboarding behavior is implemented
  by 43A1.

## Deferred

- automatic Tailscale discovery;
- remote Runtime Host lifecycle administration;
- remote endpoint attachment, detachment, or replacement;
- centralized fleet orchestration and failover;
- automatic certificate-authority operation, renewal, or rotation;
- Python automation;
- SCPI integration;
- remote media streaming; and
- diagnostic export and offline analysis.
