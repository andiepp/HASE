# ADR-0025 - Runtime-Host Identity Resolution

- Status: Accepted
- Date: 2026-07-23

---

# Context

ADR-0023 defines the northbound runtime-host API boundary.

ADR-0024 defines `RuntimeHostId` as the stable authoritative identity of one
logical HASE runtime-host installation. The identity is independent of machine
names, network addresses, Tailscale node identity, northbound listening
addresses, attached endpoints, and endpoint attachment generations. It is
identification rather than an authentication credential.

The current Phase 7 implementation provides:

- the immutable `RuntimeHostId` contract;
- immutable runtime-host snapshots containing `RuntimeHostId`;
- a runtime-host snapshot provider that receives one already resolved identity;
- stable identity across repeated snapshot captures.

Identity resolution and persistence are not yet implemented.

ADR-0024 establishes this precedence:

1. explicit identity supplied by host configuration;
2. previously persisted generated identity;
3. newly generated identity persisted before northbound publication.

The implementation must preserve this precedence without coupling northbound
application services to files, databases, operating-system settings, Tailscale,
or another persistence mechanism.

First-run generation also introduces a concurrency problem. Two startup paths
must not both observe an empty store, generate different identities, and publish
different results. An in-process lock alone cannot protect multiple processes,
containers, or future shared persistence implementations.

The persistence boundary must therefore provide an atomic create-if-missing
operation.

Identity failure behavior must also be explicit. Unreadable or malformed
persisted identity must not be treated as an empty store because silently
generating a replacement would change authoritative runtime-host identity
without approval.

---

# Decision

Runtime-host identity resolution is a transport-independent asynchronous
startup service.

It applies explicit-configuration, persisted, and generated-and-persisted
precedence. First-run creation is atomic at the persistence boundary, and no
generated identity is published until persistence succeeds.

Invalid, unreadable, or unavailable persisted identity fails safely and is
never silently replaced.

## Resolution result

Successful resolution returns an immutable result containing:

- authoritative `RuntimeHostId`;
- identity origin.

The supported origins are:

```text
ExplicitConfiguration
Persisted
GeneratedAndPersisted
```

Origin is diagnostic information. It does not change identity semantics and
does not grant authority.

## Resolver responsibility

The identity resolver owns:

- configured, persisted, and generated precedence;
- cancellation propagation;
- coordination of store reads;
- coordination of candidate generation;
- coordination of atomic first-run creation;
- selection of the authoritative identity returned by the store;
- reporting the resolution origin.

Conceptually:

```text
IRuntimeHostIdentityResolver
    ResolveAsync(
        optional configured RuntimeHostId,
        CancellationToken)
```

The resolver is asynchronous because persistence may use files, databases,
platform storage, remote configuration, or another future mechanism.

The resolver does not:

- select a persistence technology;
- choose a file or database location;
- parse application command lines;
- read environment variables directly;
- inspect machine or network identity;
- call Tailscale;
- publish the northbound API;
- authenticate clients.

## Identity store

A persistence abstraction provides:

```text
IRuntimeHostIdentityStore
    ReadAsync(CancellationToken)

    CreateIfMissingAsync(
        candidate RuntimeHostId,
        CancellationToken)
```

`ReadAsync` returns:

- the valid persisted identity; or
- no identity when the store is genuinely uninitialized.

The store must not report missing identity when persisted content is:

- present but malformed;
- unreadable;
- inaccessible;
- ambiguous;
- incompatible;
- only partially written.

Those conditions are failures.

`CreateIfMissingAsync` is atomic and returns:

- the candidate and a created outcome when this call persisted it; or
- the identity already present and an existing outcome when another startup
  path won the race.

The store never overwrites an existing identity through
`CreateIfMissingAsync`.

## Identity generator

Generation is represented behind a separate abstraction:

```text
IRuntimeHostIdGenerator
    Generate()
```

The default future generator produces:

```text
runtime-host-<lowercase canonical GUID>
```

The generator:

- creates a candidate identity only;
- does not read configuration;
- does not read or write persistence;
- does not publish the identity;
- does not decide whether its candidate becomes authoritative.

Separating generation makes precedence, storage behavior, and generated-format
behavior independently testable.

## Explicit configuration path

When an explicit `RuntimeHostId` is supplied:

1. the resolver returns it immediately;
2. the origin is `ExplicitConfiguration`;
3. the persistent generated-identity store is not read;
4. the store is not modified;
5. the generator is not called.

Configured identity has already been validated by construction of
`RuntimeHostId`.

Removing or changing an explicitly configured identity is deliberate host
reprovisioning. Configuration management must treat the resulting identity
change as intentional.

An explicitly configured identity is not copied automatically into the
generated-identity store.

## Persisted identity path

When no explicit identity is supplied:

1. the resolver reads the identity store;
2. when a valid identity exists, the resolver returns it unchanged;
3. the origin is `Persisted`;
4. the generator is not called;
5. the store is not rewritten.

Malformed or unreadable persisted identity is a failure. The resolver does not
generate a replacement.

## First-run generation path

When no explicit identity or persisted identity exists:

1. the resolver asks the generator for one candidate;
2. the resolver passes the candidate to atomic
   `CreateIfMissingAsync`;
3. the store returns the authoritative persisted identity and whether this call
   created it;
4. the resolver returns the store-provided authoritative identity;
5. the origin is `GeneratedAndPersisted` when this call created the value;
6. the origin is `Persisted` when another concurrent resolver created the value
   first.

The generated candidate is not authoritative merely because it was generated.

The candidate must not be supplied to the snapshot provider or another
northbound service before atomic persistence succeeds.

## Concurrency

First-run concurrency is resolved at the persistence boundary.

An in-process resolver lock may reduce duplicate local work but is insufficient
as the correctness mechanism because it cannot coordinate:

- multiple processes;
- multiple service instances;
- multiple containers;
- future shared stores.

The store's atomic create-if-missing result is authoritative.

When two resolvers generate different candidates concurrently:

- exactly one identity is persisted;
- both resolvers return the same persisted identity;
- only the winning resolver reports `GeneratedAndPersisted`;
- the other reports `Persisted`;
- the losing candidate is discarded.

## Failure behavior

Resolution produces no usable identity when:

- configured identity input cannot be constructed by the caller;
- store reading fails;
- persisted content is invalid;
- generation fails;
- atomic creation fails;
- the atomic result is invalid or cannot be verified;
- cancellation is requested.

Cancellation propagates normally.

The initial contract may propagate collaborator failures directly. A later
increment may introduce a dedicated identity-resolution exception with stage
information when required by production diagnostics.

There is no automatic ephemeral fallback.

An explicitly configured ephemeral development mode remains separate future
work.

## Lifetime

Identity resolution occurs during runtime-host composition before northbound
publication.

The successful immutable resolution result is owned by host composition.

The resolved `RuntimeHostId` is supplied to:

- `RuntimeHostSnapshotProvider`;
- future northbound API hosting;
- future diagnostics;
- future authentication or trust binding;
- future runtime-host discovery verification.

The snapshot provider does not call the resolver repeatedly.

The resolver does not need to retain ownership of the identity after successful
resolution.

## Persistence technology

This decision does not select:

- a file format;
- a file path;
- Windows Registry;
- a database;
- environment variables;
- secret storage;
- cloud storage;
- Tailscale metadata.

The initial resolver implementation uses test doubles to validate precedence,
atomic outcomes, cancellation, and failure propagation.

A production persistence mechanism requires a separate approved increment.

## Security

Identity resolution does not authenticate the runtime host.

The store contains public host identification rather than an authentication
secret.

Future credentials or certificates may be associated with the resolved
`RuntimeHostId`, but authentication, authorization, encryption, credential
lifecycle, and auditing remain separate decisions.

## Initial implementation boundary

The implementation sequence after this ADR is:

1. add identity-origin and resolution-result contracts;
2. add identity-generator and identity-store abstractions;
3. add the resolver abstraction;
4. implement explicit-configuration precedence;
5. implement persisted-identity precedence;
6. implement atomic generated-and-persisted resolution;
7. test concurrent create outcomes using controlled test doubles;
8. test cancellation and failure propagation.

The initial implementation does not include a production store or configuration
reader.

---

# Consequences

## Positive consequences

- Identity precedence is explicit and independently testable.
- Northbound services remain independent of persistence technology.
- Configured identity avoids unnecessary storage access.
- Valid persisted identity is never regenerated or rewritten.
- First-run races converge on one authoritative identity.
- Generated identity is never published before persistence succeeds.
- Corrupted persistence cannot silently change runtime-host identity.
- Generator, store, and resolver responsibilities remain separate.
- Future production persistence can be added without changing snapshot
  semantics.

## Negative consequences

- The persistence contract requires an atomic create-if-missing operation.
- A simple read followed by an ordinary write is insufficient.
- Store implementations must distinguish missing from malformed or unreadable
  data.
- Startup can fail when identity cannot be resolved safely.
- Additional contracts and test doubles are required before production storage
  is implemented.

## Neutral consequences

- `RuntimeHostId` remains unchanged.
- Runtime-host snapshots remain unchanged after receiving the resolved identity.
- Endpoint attachment generations retain their existing meaning.
- Protocol Version 1 remains unchanged.
- Compact Serial Protocol remains unchanged.
- Physical endpoint lifecycle ownership remains unchanged.
- Tailscale remains separate future host-discovery infrastructure.

---

# Follow-up sequence

The approved follow-up sequence is:

1. implement the resolution result and origin;
2. implement generator, store, and resolver contracts;
3. implement precedence with in-memory test doubles;
4. validate atomic race behavior;
5. validate cancellation and collaborator failures;
6. select and approve a production persistence technology;
7. integrate resolution into runtime-host composition;
8. expose origin through diagnostics where appropriate;
9. address duplicate host identities during future host discovery.

Each increment remains independently buildable and testable.
