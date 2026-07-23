# ADR-0026 - File-Based Runtime-Host Identity Store

- Status: Accepted
- Date: 2026-07-23

---

# Context

ADR-0024 defines `RuntimeHostId` as the stable authoritative identity of one
logical HASE runtime-host installation.

ADR-0025 defines transport-independent runtime-host identity resolution with
this precedence:

1. explicit identity supplied by host configuration;
2. previously persisted generated identity;
3. newly generated identity persisted before northbound publication.

ADR-0025 also defines:

- `IRuntimeHostIdentityStore`;
- asynchronous identity reads;
- atomic create-if-missing persistence;
- failure rather than silent replacement when persisted identity is invalid,
  unreadable, or unavailable;
- selection of the store-provided identity as authoritative during first-run
  races.

The accepted contracts and resolver do not choose a production persistence
technology.

Phase 7 requires one concrete store before a generated runtime-host identity
can be used safely by a production northbound host. The store must work on
Windows and Linux, preserve identity across process restarts, coordinate
multiple processes, and avoid coupling the northbound runtime API to one
application's configuration system or operating-system directory conventions.

A file store is sufficient for the first production persistence implementation
when its publication operation is atomic and its format and failure behavior
are explicit.

---

# Decision

HASE will provide a file-based implementation of
`IRuntimeHostIdentityStore`.

The implementation is named:

```text
FileRuntimeHostIdentityStore
```

It receives one explicit, fully qualified identity-file path through its
constructor.

The store owns:

- reading and validating the identity document;
- serializing a generated identity candidate;
- publishing the document atomically when the target is missing;
- returning the authoritative target identity after a first-run race;
- propagating storage, validation, and cancellation failures.

The store does not own:

- selecting an operating-system directory;
- reading host configuration;
- selecting a service account;
- parsing command-line arguments;
- reading environment variables;
- choosing container volumes;
- backup or migration policy;
- northbound publication;
- authentication or authorization;
- Tailscale integration.

## Path ownership

Runtime-host composition selects and supplies the identity-file path.

This separates persistence mechanics from application hosting policy. A
console host, Windows service, Linux service, container, test host, or future
embedded runtime host can select an appropriate persistent location without
changing the northbound identity store.

The supplied path must be fully qualified. Relative paths are rejected because
their meaning can change with process working directory.

The conventional filename is:

```text
runtime-host-identity.json
```

This ADR does not prescribe one universal parent directory.

## Document format

The identity document is UTF-8 JSON with this logical structure:

```json
{
  "formatVersion": 1,
  "runtimeHostId": "runtime-host-01234567-89ab-cdef-0123-456789abcdef"
}
```

The first supported document-format version is `1`.

The document is deliberately separate from the northbound API contract
version. Changing the storage representation does not change
`RuntimeHostApiVersion`, and changing the northbound API does not implicitly
change the identity document.

The parser requires:

- one JSON object as the root value;
- exactly one `formatVersion` property;
- exactly one `runtimeHostId` property;
- integer `formatVersion` equal to `1`;
- string `runtimeHostId` accepted by the `RuntimeHostId` constructor;
- no duplicate properties;
- no unknown properties;
- no trailing JSON content;
- valid UTF-8;
- a bounded document size.

JSON formatting whitespace is not semantically significant.

The implementation writes UTF-8 without a byte-order mark and may terminate
the document with one newline.

## Missing identity

The store reports no identity only when the configured target file is
genuinely absent.

A missing parent directory is also an uninitialized store when the target file
does not exist.

The following conditions are failures and are not reported as missing:

- malformed JSON;
- invalid UTF-8;
- an empty document;
- an unsupported format version;
- a missing, duplicate, unknown, or incorrectly typed property;
- an invalid runtime-host identity;
- an oversized document;
- an inaccessible file;
- an unreadable file;
- a directory at the configured file path;
- an I/O failure whose meaning is not target absence.

The store never deletes, repairs, truncates, or replaces an invalid target
automatically.

## Atomic create-if-missing

First-run publication uses a temporary file and an atomic non-overwriting move.

The operation is:

1. create the target parent directory when it is absent;
2. create a uniquely named temporary file in the target directory;
3. serialize the candidate identity document into the temporary file;
4. flush and close the completed temporary file;
5. move the temporary file to the target path without allowing overwrite;
6. return the candidate with outcome `Created` when the move succeeds;
7. when the target already exists, discard the temporary file, read and
   validate the target, and return its identity with outcome `Existing`.

The temporary file is created in the target directory so publication remains
on one filesystem. This is required for atomic rename semantics.

The target is never exposed as a partially written identity document by the
normal creation path.

The store does not use a long-lived lock file or an in-process lock as its
correctness mechanism. The non-overwriting atomic publication operation decides
the winner across processes.

## First-run race

When multiple processes attempt first-run creation:

- each process may serialize a different candidate into its own temporary
  file;
- exactly one non-overwriting move publishes the target;
- the winner returns the candidate with outcome `Created`;
- each loser reads the completed target;
- each loser returns the target identity with outcome `Existing`;
- all resolvers converge on the same authoritative persisted identity.

A failure indicating that the target already exists is treated as a race only
after the implementation verifies that the target now exists and can be read
successfully.

Other move or storage failures propagate.

## Temporary files

Temporary filenames are unique and implementation-owned.

The store attempts to remove its own temporary file when publication does not
succeed. Cleanup failure must not hide the original persistence failure.

An orphaned temporary file:

- is not an initialized identity store;
- is never returned as authoritative identity;
- is never selected by filename ordering or age;
- does not cause automatic replacement of the target.

General orphan cleanup is separate operational work.

## Cancellation

Cancellation is propagated through asynchronous reads, writes, and flushes.

The atomic move is the publication commit point.

- Cancellation observed before publication prevents the move.
- The store attempts to remove its temporary file after pre-publication
  cancellation.
- After the move succeeds, the identity is authoritative.
- The method returns the committed `Created` result rather than reporting
  cancellation after the side effect has become authoritative.

Reading an existing target remains cancellable.

## Durability

The temporary document is flushed before publication.

Atomic publication prevents readers from observing a partially written target.
It does not claim stronger power-loss durability guarantees than the underlying
filesystem and operating system provide.

Directory-entry synchronization and filesystem-specific durability controls
are not part of the initial cross-platform implementation.

## Security and permissions

`RuntimeHostId` is identification, not a credential.

The identity document does not contain an authentication secret. The store
therefore uses the permissions inherited from its deployment-selected
directory.

Deployment remains responsible for ensuring that unauthorized users cannot
modify runtime-host state. Authentication, authorization, integrity protection
against a privileged local attacker, and northbound transport security remain
separate concerns.

## Migration and cloning

Moving a logical runtime-host installation may deliberately include its
identity document so that the logical host retains its identity.

Copying the same identity document to two concurrently active hosts creates an
identity conflict. The file store does not attempt to detect remote clones.
Future northbound discovery and connection logic may diagnose simultaneously
reachable hosts claiming the same `RuntimeHostId`.

Deleting or replacing the identity document is an explicit reprovisioning
operation and is never performed automatically by the store.

---

# Rejected alternatives

## Current working directory

Rejected because working-directory selection is process-launch dependent and
can change between interactive execution, services, tests, and containers.

## Store-selected operating-system directory

Rejected because the reusable store should not decide whether identity belongs
to a user profile, machine-wide application data, service state, or a mounted
container volume.

Runtime-host composition owns that deployment decision.

## Environment variables or command-line parsing inside the store

Rejected because configuration acquisition is outside persistence mechanics
and outside the identity-store contract.

## Write directly to the target with `CreateNew`

Rejected because another reader could observe the target after creation but
before the document is completely written and flushed.

## Write temporary file and overwrite target

Rejected because an existing authoritative identity must never be replaced by
first-run creation.

## In-process locking

Rejected as the correctness mechanism because it cannot coordinate multiple
processes, services, containers, or future shared-store users.

## Treat malformed content as missing

Rejected because generating a replacement would silently change authoritative
runtime-host identity.

## Unversioned plain text

Rejected because a versioned envelope provides explicit compatibility,
unambiguous validation, and room for controlled future storage evolution.

## Persist explicitly configured identity automatically

Rejected by ADR-0025. Explicit configuration has precedence but is not copied
into the generated-identity store.

## Registry, credential vault, or platform-specific key store

Rejected for the initial implementation because runtime-host identity is not a
secret and HASE requires one cross-platform persistence boundary.

---

# Implementation sequence

Implementation proceeds in small, independently buildable increments:

1. add this ADR;
2. add internal versioned document serialization and strict parsing;
3. add file-store construction and read behavior;
4. add atomic create-if-missing behavior;
5. add first-run concurrency tests;
6. integrate path selection and identity resolution in a concrete runtime-host
   composition root;
7. update ProjectStatus and Roadmap after verification.

No increment changes the precedence or identity semantics established by
ADR-0024 and ADR-0025.

---

# Consequences

## Positive

- Generated runtime-host identity survives process restarts.
- Windows and Linux hosts use the same persistence implementation.
- Runtime-host applications retain control over deployment-specific paths.
- Readers do not observe partially written target documents during normal
  creation.
- Multiple processes converge on one authoritative identity.
- Invalid persisted state fails safely instead of changing identity silently.
- The on-disk format is explicit and versioned.
- The northbound API remains independent of filesystem and hosting policy.

## Costs

- Runtime-host composition must select and configure a persistent path.
- Startup can fail because of invalid documents, permissions, storage failures,
  or unsupported versions.
- Temporary files can remain after process termination or power loss.
- Filesystem atomicity and durability remain dependent on the host filesystem.
- Deployment and migration procedures must preserve or deliberately replace
  the identity document.

---

# Verification requirements

Automated verification must cover:

- rejection of null, empty, relative, and otherwise invalid paths;
- missing target returns no identity;
- valid version-1 document returns its identity;
- strict rejection of malformed and incompatible documents;
- inaccessible and non-file targets fail;
- creation produces the canonical version-1 document;
- creation never overwrites an existing target;
- a first-run race returns one `Created` result and remaining `Existing`
  results;
- all racing callers return the same authoritative identity;
- failed or cancelled creation does not publish a partial target;
- cancellation tokens reach asynchronous file operations where supported;
- temporary cleanup does not replace or redefine the authoritative target.

Integration verification must demonstrate that resolving without explicit
configuration:

1. generates and persists identity on first startup;
2. returns the same persisted identity after restart;
3. supplies that identity to runtime-host snapshots before northbound
   publication.
