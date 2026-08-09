# ADR-0050 — Python Automation Boundary

- Status: Accepted; Increment 50I implementation and closure validation
- Date: 2026-08-07

## Context

HASE exposes descriptor-driven Runtime Host inventory, cached and authoritative
Property reads, Property writes, Command execution, live observations, and
separately authorized Runtime Host diagnostics through its versioned northbound
gRPC boundary. The supported Client implementation is currently .NET-based and
the interactive applications are WPF applications.

Automation requires a supported Python boundary without giving scripts direct
ownership of physical transports or adding a second operational API. Python
must preserve the established mutual-TLS, authorization, attachment-generation,
operation-result, uncertain-mutation, recovery, and live-only observation
semantics. In particular, a convenient scripting interface must not turn a
transport failure into an automatic repeat of a Property write or Command.

## Decision

### Boundary and ownership

The Python API is an external Client of the existing versioned Runtime Host
gRPC API. It does not run inside the Runtime Host and does not access USB,
serial, SCPI, TCP endpoints, discovery, attachment, recovery, configuration,
or hardware directly. The Runtime Host retains exclusive ownership of physical
sessions and endpoint lifecycle.

The package distribution name is `hase-client` and its import namespace is
`hase`. The public API is asyncio-native. One Python session represents one
configured Runtime Host profile; multi-host automation uses independent
sessions and does not combine attachment identities or generations.

### Contract custody

Python protobuf messages and gRPC stubs are generated reproducibly from the
authoritative version-1 protobuf source already owned by
`Hase.Runtime.Remote.Grpc.Contracts`. The wire model is not manually copied.
Generated code is an implementation detail below descriptor-driven Python
models and operations.

The Python Client rejects an unsupported API major version, unspecified or
unknown required enum values, malformed identifiers, invalid timestamps,
invalid union selections, and structurally invalid stream ordering. Unknown
protobuf fields retain their normal forward-compatible behavior.

### Security and configuration

Every session uses HTTPS over HTTP/2 with mutual TLS. Client certificate,
private-key, and exact trusted-server certificate material remain external to
the repository and Python package. A strict external profile references those
files by absolute path. Profiles reject unknown fields, non-HTTPS addresses,
embedded credentials, paths, queries, fragments, missing credential files, and
unsupported format versions.

The profile, certificate material, private network address, principal, policy,
and permission set are deployment custody. They are never emitted by normal
API representations, exception text, examples, tests, source archives, or
logs. Credential conversion or export requires a separate explicit offline
provisioning increment; the operational API never provisions credentials.

Authentication does not imply authorization. The existing permission for each
RPC continues to be evaluated by the Runtime Host. Remote diagnostics retain
their separate `diagnostics.subscribe` authorization and explicit Runtime Host
enablement. The Python diagnostic stream never weakens either gate.

### Targeting and descriptor access

Every active Property and Command operation carries the endpoint identity,
attachment generation, instrument identity, and member identity obtained from
one coherent snapshot or observation state. Display names are never used as
operational identity. A stale target is not automatically retargeted to a new
generation.

The API provides both explicit identifier-based targeting and descriptor-based
navigation. Descriptor navigation resolves to the same immutable,
generation-qualified target before an RPC is transmitted. Ambiguous or absent
descriptor paths fail locally and cause no RPC.

Boolean, numeric, string, and byte-array values map without lossy string
conversion. Numeric range and supported-value validation occurs locally from
the authoritative descriptor and remains repeated by the Runtime Host and
endpoint boundaries.

### Reads, mutations, and uncertainty

Snapshot, cached Property read, and authoritative Property read operations use
explicit finite deadlines. Returned application status is validated separately
from gRPC transport status.

Each explicit Property write or Command call transmits at most one mutation
RPC. The Python library never automatically retries, redirects, queues, or
replays a mutation. Timeout, connection loss, cancellation after transmission,
or another failure that cannot prove non-execution produces an explicit
uncertain outcome. A caller must deliberately perform an authoritative read or
obtain a fresh snapshot before deciding whether another mutation is safe.

Read-only session establishment and observation recovery may use a later
bounded policy, but that policy cannot be shared with or applied to mutations.

### Observation semantics

Observation is server streaming and asyncio-native. The first item must be the
initial snapshot. Later subscription-local sequences must increase strictly.
Attachments are keyed by endpoint identity plus attachment generation.

Events and observations are live-only and are never replayed. A new
subscription begins with a new initial snapshot. A gap or invalid ordering
terminates that subscription; recovery, when implemented, opens a fresh
subscription and never synthesizes missed Events or repeats an operation.

### Supported Python baseline

The initial supported development line is 64-bit CPython 3.13 on the validated
Windows Client environment. The minimum package metadata remains Python 3.12
or later so the package does not depend unnecessarily on a Python 3.13-only
language feature. CPython 3.13 is the line used for contract generation,
automated testing, packaging, and physical validation until a later explicit
compatibility decision expands that matrix.

Python dependencies are installed only into a repository-local `.venv`. HASE
does not install or upgrade global Python packages. The virtual environment,
generated build output, caches, credentials, and external profiles are not
committed or included in source or deployment archives.

## Increment plan

1. 50A–50C — Decision, generated contract, profile, channel, and credential provisioning.
2. 50D–50H — Snapshot, reads, mutations, Commands, observations, and cached reads.
3. 50I — Strict diagnostic observation, temporary authorization, physical validation,
   exact disabled-state restoration, documentation reconciliation, and closure.

## Consequences

- Existing Runtime Host, endpoint, transport, and northbound contracts remain
  authoritative and unchanged by Increment 50A.
- Python automation inherits the same security and device-authoritative model
  as the .NET and WPF Clients.
- Generated protobuf code cannot become a competing hand-maintained contract.
- Asyncio supports long-lived observation without background-thread ownership
  hidden from scripts.
- Users must make mutation retry decisions explicitly after authoritative
  reconciliation.
- Pure-Python mTLS requires externally provisioned certificate and private-key
  files; Windows certificate-store export is not implicit.
- Synchronous convenience APIs, notebooks, persistent data logging, Linux
  provisioning, and public package publication remain later decisions.

## Diagnostic observation and closure

`RuntimeHostClient.observe_diagnostics()` exposes the seventh and final
version-1 Runtime Host RPC as one caller-owned asynchronous stream. Every
record is projected strictly into immutable values and retains both sequence
domains, exact Runtime Host and endpoint/generation scope, optional direction
and operation correlation, immutable details, and exact captured byte
fragments with original-count and truncation evidence. A malformed record,
sequence gap, authorization denial, transport failure, or cancellation ends
the stream. The library never reconnects, resubscribes, replays, or fills a
gap.

Increment 50I temporarily appends only `diagnostics.subscribe` to the exact
Python principal and enables remote diagnostics at the Bytes ceiling. Both
the policy and application profile are revision-locked and atomically
replaced; exact rollback bytes and access control are verified. Physical
closure requires one safe KEL-103 authoritative read while CC/OFF with the
external supply output OFF. Operational, Protocol, and exact Bytes records
must retain Runtime Host and endpoint/generation scope, the request must end
in `0D`, and the final correlated receive fragment must end in `0A`.

After that observation the Runtime Host is stopped and the paired restore
operation reinstates the exact pre-50I policy and profile bytes. Closure
requires `diagnostics.subscribe` to be absent, remote diagnostics to be
disabled, transaction artifacts to be absent, and all Python, focused
provisioning, and full .NET tests to pass.

## Increment 50A validation

Increment 50A is documentation and read-only environment characterization
only. It adds no executable production path, opens no network connection,
loads no credential, changes no authorization policy, invokes no endpoint
operation, and changes no KEL-103 state.

The solution must continue to build in Visual Studio 2026 Release and all
existing automated tests must pass unchanged. Characterization is performed
from an ordinary PowerShell window and prints only non-sensitive interpreter
and package metadata. It performs no package installation.

Read-only characterization on 2026-08-07 established:

```text
Interpreter     : CPython 3.13.1
Executable      : per-user 64-bit Python 3.13 installation
Architecture    : 64-bit
Operating system: Windows build 10.0.19045
SSL             : OpenSSL 3.0.15 3 Sep 2024
grpcio          : not installed
protobuf        : not installed
grpcio-tools    : not installed
```

The per-user executable path is deployment-specific and is deliberately not
recorded. The missing packages are the expected clean starting state. No
package was installed, no network connection was opened, no credential was
accessed, and no Runtime Host or endpoint operation occurred.

Visual Studio 2026 Release built successfully. One timing-sensitive runtime
transport test initially timed out, passed when run individually, and the
complete suite then passed at the unchanged authoritative baseline of 5,726
tests. Increment 50A is accepted on that evidence.
