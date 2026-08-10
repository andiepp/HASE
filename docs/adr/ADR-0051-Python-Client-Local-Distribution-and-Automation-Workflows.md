# ADR-0051 — Python Client Local Distribution and Automation Workflows

- Status: Accepted; Increment 51F documentation and closure
- Date: 2026-08-10

## Context

ADR-0050 established the supported external Python automation boundary and
physically validated all seven version-1 Runtime Host RPCs. A source checkout
and editable development environment are not an operational distribution
model, however. HASE also needs reproducible local packaging, isolated
installed automation, guarded workflows, and explicit multi-host operation
from the Client computer without publishing credentials or packages globally.

The validated deployment contains three Windows computers: the main
development computer with the Desktop Runtime Host, a MiniPC Runtime Host, and
the Laptop Client. Each Runtime Host owns its physical endpoints and has a
different server identity. Python automation must preserve that separation and
must not reuse WPF Client credentials, Runtime Host server keys, or another
computer's Python private key.

## Decision

### Local package distribution

`hase-client` is distributed locally as a versioned pure-Python wheel. The
PowerShell build tool creates the wheel plus a sorted content record and an
adjacent SHA-256 record. It rejects credentials, profiles, rollback evidence,
caches, local paths, source-control content, and other deployment material.
There is no PyPI upload and no global Python installation.

An installed-package validator creates a fresh isolated environment, installs
without editable-source access, verifies the complete public API, generated
protobuf messages and gRPC client, and all seven version-1 RPC bindings, and
then removes the environment. A separate installer verifies the adjacent hash
before creating a private persistent environment with an installed launcher
and a non-sensitive installation manifest.

The accepted package progression is:

| Version | Added installed capability |
| --- | --- |
| `0.1.0` | Local wheel and isolated installed-package validation |
| `0.2.0` | Persistent private automation installation and Health workflow |
| `0.3.0` | Guarded same-value KEL-103 Property write |
| `0.4.0` | Guarded same-state KEL-103 CC Command |
| `0.5.0` | Installed MiniPC authoritative A0 Property read |
| `0.6.0` | Explicit Laptop Desktop/MiniPC target registry |

### Workflow safety

Read-only workflows require no mutation confirmation. The two installed
mutation workflows require distinct explicit confirmation switches, verify a
safe KEL-103 CC/OFF starting state, transmit exactly one mutation, and perform
authoritative reconciliation. They never retry, reconnect, replay, change a
setpoint, activate the load, or treat a later matching state as proof that an
uncertain Command executed.

Target selection is explicit. A launcher accepts either one direct profile or
the pair of an external target registry and exact target identifier. Supplying
both modes, neither mode, an incomplete pair, or an unknown target fails
locally. There is no discovery, fan-out, failover, redirection, or automatic
selection between Runtime Hosts.

### Three-computer credential custody

The Desktop and MiniPC Runtime Hosts retain distinct server identities,
certificate pins, enrollment and authorization state. The Laptop holds two
strict external Python profiles and two distinct client credentials. The
MiniPC-local Python identity is not reused by the Laptop, and neither Python
identity reuses a WPF Client credential.

The MiniPC uses a private CPython 3.13.1 runtime without installer registration
or PATH mutation. A dedicated non-exportable MiniPC client authority issues
the MiniPC-local and Laptop-to-MiniPC Python credentials. Provisioning,
publication, transfer, import, recovery, and profile-custody repair are
revision-locked transactions with protected external evidence and sanitized
journals. Runtime Host processes remain stopped during those transactions.

The Laptop target registry contains exactly `desktop-runtime-host` and
`minipc-runtime-host`. It stores profile paths, not addresses or credential
contents. The profiles, registry, credentials, private keys, trusted-server
certificates, installation, and rollback evidence remain external to the
repository and wheel.

### Local artifacts and cleanup

The authoritative accepted wheel is `hase-client 0.6.0`, with SHA-256:

```text
4abd42ccb529703560c3f8e400b50c9cd290d319671ed9040d13ac131eb4df2c
```

The Laptop installation manifest records that exact package version and hash.
Transferred wheel copies and hash records are removed after installation.
After successful credential import and physical validation, the MiniPC transfer
archive and duplicate four-file staging custody are removed. Protected
preparation and rollback evidence and the MiniPC profile template are retained.

No credential, profile, private address, local absolute path, rollback content,
or private key is recorded in this ADR.

## Physical validation

The installed `0.6.0` launcher on the Laptop selected each target explicitly
through the two-target registry.

For the Desktop Runtime Host, the Health workflow received a valid snapshot
with all three endpoints Ready, four Instruments, sixteen Properties, ten
Commands, and two Events. For the MiniPC Runtime Host, Health received a valid
snapshot with its one Arduino endpoint Ready, one Instrument, two Properties,
one Command, and one Event.

The Laptop then executed the read-only MiniPC authoritative Property workflow.
It resolved the current Arduino A0 target, completed one authoritative read,
validated the result, and closed the channel. All three workflows succeeded
without retry, reconnection, mutation, diagnostics, authorization change, or
automatic target selection. Hashes of all nine protected Laptop registry,
profile, credential, key, and server-trust files remained unchanged.

The Desktop Runtime Host remained Running with its expected endpoints Ready;
the KEL-103 remained CC/OFF and the laboratory supply output remained OFF. The
MiniPC Runtime Host remained Running with its Arduino endpoint Ready. Both
Runtime Hosts were stopped after validation, and the WPF Client and installed
automation were stopped.

## Closure evidence

Increment 51F closes at the following regression baseline:

```text
494 Python tests pass
161 focused credential-provisioning tests pass
5,897 complete .NET tests pass in Release using HASE.slnx
```

The Desktop, MiniPC, and Laptop repositories are clean and synchronized with
`origin/main`. Remote Runtime Host diagnostics remain disabled. No temporary
authorization change, diagnostic grant, transaction journal, transferred
wheel, transferred hash record, MiniPC credential archive, or duplicate MiniPC
private-key staging custody remains.

## Consequences

- Python automation can run from the Laptop and reach either approved Runtime
  Host through explicit target selection.
- Local wheel distribution is reproducible and hash-bound without creating a
  public package channel.
- Installed automation is independent of the repository checkout and global
  Python state.
- Runtime Hosts continue to own hardware, endpoint lifecycle, and operation
  authority.
- Credentials and deployment configuration remain external, host-specific,
  protected custody.
- Mutation uncertainty, no-retry, no-replay, and live-only semantics from
  ADR-0050 remain unchanged.
- Credential rotation, revocation, public package publication, additional
  target registries, Linux deployment, scheduling, and unattended production
  automation require later explicit decisions.
