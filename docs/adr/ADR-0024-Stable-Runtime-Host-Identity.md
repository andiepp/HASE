# ADR-0024 - Stable Runtime-Host Identity

- Status: Accepted
- Date: 2026-07-23

---

# Context

ADR-0023 defines the northbound runtime-host API boundary. It establishes that
local and remote applications use the runtime model owned by a HASE runtime
host, while that host remains the sole owner of every physical endpoint
connection, southbound protocol or adapter session, synchronization service,
recovery supervisor, notification route, and attachment lifetime.

The initial Phase 7 implementation provides:

- northbound API contract versioning;
- opaque endpoint attachment generations;
- immutable published endpoint snapshots;
- authoritative endpoint-inventory projection;
- endpoint listing and lookup;
- immutable runtime-host snapshots containing API version and published
  endpoints.

The runtime-host snapshot does not yet contain runtime-host identity.

A stable host identity is required before:

- applications can remember a particular runtime host;
- a client can verify that it reconnected to the intended host;
- more than one runtime host can be represented safely;
- LAN, manually configured, or future Tailscale-assisted host discovery can
  deduplicate verified hosts;
- credentials can later be associated with a specific host;
- host migration can be distinguished from accidental configuration cloning.

Several available machine and network identifiers are unsuitable as
authoritative HASE runtime-host identity:

- IP addresses can change and one host may have several addresses;
- DNS names can change and may not be globally unique;
- Windows or Linux machine names can change and do not necessarily represent
  one HASE installation;
- a Tailscale node identity belongs to the Tailscale network layer;
- northbound listening addresses describe reachability;
- process identifiers and generated startup values change across restarts.

HASE must distinguish authoritative runtime-host identity from candidate
reachability metadata in the same way that physical endpoint identity is
distinguished from IP addresses, COM ports, USB metadata, and discovery
instance names.

Runtime-host identity also must not be confused with authentication.
Identification states which logical host is represented. Authentication
establishes whether a peer is authorized to claim or access that identity.

---

# Decision

Every northbound-capable HASE runtime host has one stable `RuntimeHostId`.

`RuntimeHostId` identifies one logical HASE runtime-host installation. It is an
immutable HASE identity value represented as a non-empty normalized string.

The runtime-host identity is resolved before northbound publication and remains
unchanged for the lifetime of the running host.

## Identity semantics

`RuntimeHostId` is:

- stable across process restarts;
- stable across application upgrades;
- stable across northbound client reconnections;
- independent of attached physical endpoints;
- independent of endpoint attachment generations;
- independent of endpoint recovery and transport replacement;
- independent of IP addresses and DNS names;
- independent of the operating-system machine name;
- independent of Tailscale node identity and node name;
- independent of the northbound listening address;
- independent of API contract version;
- not an authentication credential.

Attaching or detaching endpoints does not change runtime-host identity.

Changing LAN, Wi-Fi, VPN, Tailscale, DNS, or listening configuration does not
change runtime-host identity.

## Representation

The public identity contract is a dedicated immutable `RuntimeHostId`.

It follows the established HASE identity model:

- its textual value must not be null, empty, or whitespace;
- surrounding whitespace is normalized away;
- equality is value based;
- textual representation returns the normalized value.

Automatically generated identities use this canonical form:

```text
runtime-host-<lowercase canonical GUID>
```

Example:

```text
runtime-host-58c50d84-c4ad-47a0-b7c6-1eeed3483593
```

The canonical generated form is suitable for configuration, diagnostics,
logging, persistence, and future wire representation.

The contract accepts an explicitly configured non-empty HASE host identity. It
does not require every valid configured identity to use the generated GUID
form.

## Display name

Human-readable naming is separate from identity.

For example:

```text
RuntimeHostId : runtime-host-58c50d84-c4ad-47a0-b7c6-1eeed3483593
Display name  : Workshop Runtime
```

A display name may be changed without changing `RuntimeHostId`.

Display-name configuration and publication are separate future work and are
not required by the initial identity increment.

## Identity resolution

Runtime-host identity is resolved once during host startup using this
precedence:

1. an explicit identity supplied by host configuration;
2. a previously persisted generated identity;
3. a newly generated identity that is persisted successfully before
   northbound publication.

After resolution, one immutable `RuntimeHostId` is supplied to northbound
application services.

The runtime-host snapshot provider does not:

- inspect host configuration;
- read or write identity files;
- inspect the machine name;
- inspect network interfaces;
- call Tailscale;
- generate a new identity for each snapshot;
- decide configuration precedence.

Identity resolution and persistence belong to separate services introduced
through later approved increments.

## Persistence requirement

A newly generated production runtime-host identity must be persisted
successfully before the northbound host becomes visible to applications.

If a stable identity cannot be resolved or a newly generated identity cannot be
persisted, production northbound startup fails safely.

The host must not silently publish a temporary identity that changes after
restart.

A future explicitly configured ephemeral development mode may permit a
non-persistent identity. Ephemeral behavior must be deliberate, visible in
diagnostics, and never an automatic fallback.

Ephemeral mode is not part of the initial identity implementation.

## Migration

Preserving `RuntimeHostId` while moving a complete HASE runtime-host
installation to another computer is valid when the former installation is
retired.

This represents migration of the same logical runtime host.

Machine replacement, operating-system replacement, IP-address changes, and
Tailscale-node replacement therefore do not require a new HASE identity when
the persisted host configuration is intentionally migrated.

## Cloning and duplicate identities

Running two active runtime hosts with copied configuration and the same
`RuntimeHostId` is invalid.

HASE cannot prevent every offline configuration-copy error. Future runtime-host
discovery and client policy must:

- treat simultaneously reachable hosts claiming the same identity as a
  conflict;
- never silently merge their state;
- never select one automatically as the authoritative replacement;
- surface sufficient information for explicit diagnosis and resolution.

Duplicate-runtime-host detection policy is separate future work.

## Relationship to Tailscale

Tailscale supplies candidate reachability metadata, such as:

- Tailscale node identity;
- node or DNS name;
- network addresses;
- online state.

Those values do not become `RuntimeHostId`.

Future Tailscale-assisted runtime-host discovery follows this conceptual
sequence:

```text
Tailscale candidate
    -> connect to northbound API
    -> read authoritative RuntimeHostId
    -> verify expected identity when one was selected or configured
    -> publish or reject the verified runtime-host candidate
```

Tailscale detection identifies reachable candidates. The HASE northbound API
provides authoritative HASE runtime-host identity.

Tailscale remains optional. The same identity semantics apply over localhost,
LAN, manually configured addresses, other VPNs, and future discovery
mechanisms.

## Security

`RuntimeHostId` is identification, not proof of identity and not authorization.

Knowing a runtime-host identity grants no access.

The future security design may bind credentials, certificates, or trust records
to `RuntimeHostId`. Such binding does not turn the identifier itself into a
secret.

Network reachability, a matching claimed identity, or presence on the same
Tailscale network must not independently grant HASE authority.

Authentication, authorization, encryption, credential lifecycle, and auditing
remain separate Phase 7 architecture decisions.

## Runtime-host snapshots

`PublishedRuntimeHostSnapshot` exposes the resolved `RuntimeHostId` together
with:

- northbound API contract version;
- the current published endpoint snapshots.

`RuntimeHostSnapshotProvider` receives the already resolved identity through its
constructor.

Every snapshot captured by one provider instance exposes the same
`RuntimeHostId`.

The provider does not regenerate, reload, replace, or persist the identity.

## Process and attachment lifetimes

`RuntimeHostId` identifies the stable logical host.

It does not replace:

- northbound connection identity;
- process-instance identity;
- client-session identity;
- physical `EndpointId`;
- endpoint attachment generation;
- protocol correlation identity.

Endpoint attachment generations continue to protect operations from crossing
attachment lifetimes. A stable runtime-host identity does not make an ended
endpoint attachment current again.

Additional process-instance or host-startup generation semantics require a
separate future decision if evidence shows they are needed.

## Initial implementation boundary

The first implementation increment after this ADR:

- adds the immutable `RuntimeHostId` contract;
- validates normalization and value equality;
- adds `RuntimeHostId` to `PublishedRuntimeHostSnapshot`;
- requires a resolved `RuntimeHostId` in `RuntimeHostSnapshotProvider`;
- verifies stable identity across repeated snapshot captures.

It does not implement:

- configuration loading;
- identity generation policy;
- persistent storage;
- migration tooling;
- duplicate-host detection;
- Tailscale discovery;
- authentication or authorization;
- display-name configuration.

---

# Consequences

## Positive consequences

- Applications can identify one logical runtime host across process restarts.
- Host identity remains stable when network topology changes.
- Host identity remains separate from physical endpoint identities.
- Future LAN and Tailscale discovery can verify authoritative HASE identity.
- Intentional host migration can preserve application associations.
- Endpoint attachment generations retain their separate lifecycle meaning.
- Snapshot providers remain independent of persistence and network mechanisms.
- Future credentials can be associated with a stable public identifier.
- Duplicate host identities have explicit conflict semantics.

## Negative consequences

- Production northbound startup requires stable identity resolution.
- Generated identities require durable persistence.
- Configuration cloning can create identity conflicts that must be diagnosed.
- Migration must deliberately preserve identity and retire the old host.
- Additional services are required for configuration and persistence.

## Neutral consequences

- Existing endpoint attachment and recovery behavior remains unchanged.
- Protocol Version 1 remains unchanged.
- Compact Serial Protocol remains unchanged.
- Physical endpoint discovery remains local to the runtime host.
- Tailscale remains optional reachability and discovery infrastructure.
- Runtime-host display names remain future work.
- Authentication and authorization remain separate decisions.

---

# Follow-up sequence

The approved incremental sequence is:

1. implement and test `RuntimeHostId`;
2. add the resolved identity to runtime-host snapshots;
3. define the identity-resolution contract;
4. define explicit configuration input;
5. define persistent generated-identity storage;
6. define startup failure and diagnostics;
7. define duplicate-host behavior with future runtime-host discovery;
8. define credential binding with the northbound security architecture;
9. define Tailscale-assisted host discovery separately.

Each increment remains independently buildable and testable.
