# HASE Python Client

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. The current implementation establishes an isolated package toolchain and
package-internal generated Python bindings for the authoritative Runtime Host
protobuf contract. It does not yet open a Runtime Host connection, load
credentials, or invoke hardware.

## Development environment

From this directory in an ordinary PowerShell window:

```powershell
.\tools\Initialize-HasePythonDevelopment.ps1
.\tools\Test-HasePythonDevelopment.ps1
```

Initialization requires 64-bit CPython 3.12 or 3.13 and creates `.venv` only
inside this directory. It installs the exact versions recorded in
`requirements-development.txt`, then installs this package as an editable
package without resolving dependencies a second time.

The virtual environment is local development state. It is excluded from Git
and must not be copied into source or deployment archives.

## Generated Runtime Host contract

The only authoritative protobuf source is:

```text
src\Hase.Runtime.Remote.Grpc.Contracts\Protos\runtime_host_remote_api_v1.proto
```

Generate package-internal Python bindings from the repository root contract:

```powershell
.\tools\Generate-HasePythonContracts.ps1
```

Verify that committed bindings match a fresh generation byte for byte:

```powershell
.\tools\Test-HasePythonContractsCurrent.ps1
```

The generator uses a virtual protobuf source mapping so generated imports use
`hase._generated`. It does not copy or maintain a second protobuf source. The
generated modules are committed so package consumers do not require
`grpcio-tools`.

Generated contract types remain internal. The public `hase` namespace exposes
no Runtime Host operation yet. Although the complete wire contract includes
the separately authorized diagnostic stream, ADR-0050 excludes it from the
initial public Python API.

## Current scope

The package currently provides:

- distribution name `hase-client`;
- import namespace `hase`;
- an isolated and repeatable development dependency set;
- reproducible version-1 protobuf and gRPC bindings; and
- package and generated-module import tests.

Secure sessions, snapshots, Properties, Commands, observations, deployment
profiles, and certificate handling are added only by later approved increments.

