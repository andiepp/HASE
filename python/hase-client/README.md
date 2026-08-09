# HASE Python Client

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. The current implementation establishes an isolated package toolchain,
package-internal generated Python bindings, a strict external Runtime Host
profile model, and explicit Windows credential provisioning and recovery. It
does not yet open a Runtime Host connection or invoke hardware.

## Development environment

From this directory in an ordinary PowerShell window:

```powershell
.\tools\Initialize-HasePythonDevelopment.ps1
.\tools\Test-HasePythonDevelopment.ps1
```

Initialization requires 64-bit CPython 3.12 or 3.13 and creates `.venv` only
inside this directory. The virtual environment is excluded from Git and must
not be copied into source or deployment archives.

## Generated Runtime Host contract

The only authoritative protobuf source is:

```text
src\Hase.Runtime.Remote.Grpc.Contracts\Protos\runtime_host_remote_api_v1.proto
```

Generate package-internal Python bindings and verify freshness:

```powershell
.\tools\Generate-HasePythonContracts.ps1
.\tools\Test-HasePythonContractsCurrent.ps1
```

Generated modules remain internal. Descriptor-level parity tests lock the
reviewed version-1 wire shape.

## Strict external profile

`load_runtime_host_profile` reads one versioned JSON profile by absolute path.
It requires an explicit HTTPS IP address and absolute paths to three distinct
external files: the Client certificate chain, Client private key, and exact
trusted-server certificate. Increment 50C1 checks file custody only and does
not parse credential bytes.

## Mutual-TLS channel

`open_runtime_host_channel` creates one asyncio gRPC channel from a validated
profile. Credential reads are bounded, the exact trusted Runtime Host
certificate is supplied as the channel trust anchor, TLS authority is never
overridden, readiness has an explicit timeout, and opening is never retried.
The returned `RuntimeHostChannel` is an async context manager with deterministic
concurrent and repeated close behavior. Channel failures expose only sanitized
codes and never include credential bytes or deployment paths. Increment 50D1
does not invoke an RPC or connect during automated validation.

## Credential-provisioning readiness

The installed WPF Client private key is intentionally non-exportable and must
not be reused for Python. Python automation requires a separate Client identity
issued and enrolled through an explicit later provisioning increment.

Before that increment, run the read-only Windows readiness probe against the
retained external Desktop validation configuration:

```powershell
.\tools\Test-HasePythonCredentialProvisioningReadiness.ps1 `
    -DesktopConfigurationPath "<external desktop-private-network.json>"
```

The probe verifies the configured server credential, unique signing root and
private signing-key availability, matching Current User trust anchor, and
strict enrollment-document custody. It prints only fixed Boolean results. It
does not inspect the WPF private key, create or export a credential, modify an
enrollment, print deployment values, connect to the Runtime Host, or perform a
hardware operation.

The configuration, enrollment, profile, and every credential remain external
deployment state. They must not be committed, included in archives, printed,
or logged.

The .NET provisioning boundary can now prepare a revision-locked candidate set
entirely in memory after an approved plan has been created. It revalidates the
dedicated credential, signing root, source revisions, enrollment, authorization
policy, and Python profile before returning five disposable candidate buffers.
This preparation step does not publish files, create backups or a journal,
modify certificate stores, or connect to a Runtime Host.

An approved prepared set can now be published through a durable five-file
transaction. A bounded metadata-only journal is made durable before staging,
then updated after the candidate files have been staged and flushed and after
every publication boundary. Authorization is published last. Ordinary failures
restore the exact pre-transaction files and security metadata. A retained
journal represents an interrupted or committed-cleanup case; another
publication remains blocked until the explicit recovery boundary validates the
expected five target paths and all retained hashes. Uncommitted transactions
are rolled back; committed transactions retain their published candidates and
complete cleanup. Ambiguous, corrupt, substituted, or hash-mismatched evidence
is left untouched for operator review.

## Credential-provisioning operator

The Windows-only .NET operator composes the reviewed planning, preparation,
publication, and recovery boundaries without exposing credential or deployment
contents. Every path and deployment identifier is explicit; no security path is
discovered or defaulted. Successful provisioning reports only the plan and
transaction identifiers, replacement status, and a fixed withholding marker.
Failures report only a sanitized error code.

Run the operator from the repository root with `dotnet run --project
src/Hase.Python.CredentialProvisioning.Operator -c Release --`, followed by one
of these operations:

```text
provision --signing-root-thumbprint <value> --trust-policy-id <value> --source-profile <absolute-path> --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path> --expected-authorization-policy-sha256 <value> --validity-days <1-90> [--allow-replacement]

recover --provisioning-directory <absolute-path> --certificate <absolute-path> --private-key <absolute-path> --profile <absolute-path> --enrollment <absolute-path> --authorization-policy <absolute-path>
```

`--allow-replacement` authorizes replacement only for that invocation. A
retained journal blocks another publication until the exact five targets are
supplied to the explicit `recover` operation. The operator does not connect to
a Runtime Host or modify a certificate store.

Before physical publication, run
`tools\Initialize-HasePythonPhysicalProvisioningValidation.ps1` with explicit
absolute paths for the retained Desktop configuration, an existing public
Runtime Host certificate, authorization policy, provisioning directory, three
Python output files, a new external Python profile-template path, and a new
rollback directory outside both the repository and provisioning directory. The
installed Client and its configuration are not required. The template path
must not already exist and
must remain outside the repository. The script creates that template from only
the authoritative Desktop Runtime Host binding, the explicitly selected public
trusted-server certificate path, and the planned new Python certificate and
private-key output paths. It rejects a public certificate that differs from
the active Runtime Host certificate or contains a private key. It never opens,
exports, copies, or references the WPF Client certificate or private key.
The script requires the Runtime Host and Client to be stopped and the default
branch to be clean and synchronized. It reuses the strict readiness probe,
rejects transaction artifacts and reparse points, and creates a Current-User-
only rollback capture containing exact content, SHA-256, security descriptors,
and the withheld operator input document. Only fixed Boolean readiness results
are displayed. The rollback directory is external security state and must not
be committed, copied into a source archive, or displayed.

## Current scope

The package currently provides:

- distribution name `hase-client` and import namespace `hase`;
- reproducible version-1 protobuf and gRPC bindings;
- byte-exact freshness and descriptor-level parity validation;
- an immutable strict external Runtime Host profile model; and
- an asyncio mutual-TLS channel lifecycle with bounded readiness; and
- dedicated-Python-identity provisioning, durable five-file publication, and
  an explicit operator and interrupted-publication recovery boundaries.

Mutual-TLS channels, snapshots, Properties, Commands, and observations require
later approved increments.
