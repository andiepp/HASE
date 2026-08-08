# HASE Python Client

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. The current implementation establishes an isolated package toolchain,
package-internal generated Python bindings, a strict external Runtime Host
profile model, and read-only Windows credential-provisioning readiness
characterization. It does not yet open a Runtime Host connection, export a
credential, or invoke hardware.

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

## Current scope

The package currently provides:

- distribution name `hase-client` and import namespace `hase`;
- reproducible version-1 protobuf and gRPC bindings;
- byte-exact freshness and descriptor-level parity validation;
- an immutable strict external Runtime Host profile model; and
- read-only dedicated-Python-identity provisioning readiness characterization.

Dedicated identity creation, enrollment, protected PEM delivery, mutual-TLS
channels, snapshots, Properties, Commands, and observations require later
approved increments.

