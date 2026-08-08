# HASE Python Client

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. The current implementation establishes an isolated package toolchain,
package-internal generated Python bindings, and a strict external Runtime Host
profile model. It does not yet open a Runtime Host connection, parse credential
bytes, or invoke hardware.

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

Generate package-internal Python bindings and verify freshness:

```powershell
.\tools\Generate-HasePythonContracts.ps1
.\tools\Test-HasePythonContractsCurrent.ps1
```

The generator uses a virtual protobuf source mapping so generated imports use
`hase._generated`. It does not copy or maintain a second protobuf source. The
generated modules are committed so package consumers do not require
`grpcio-tools`.

Descriptor-level parity tests lock the reviewed service methods, streaming
shapes, value union, generation-qualified targets, observation unions,
descriptor payloads, imported duration type, and enum assignments.

## Strict external profile

`load_runtime_host_profile` reads one versioned JSON profile by absolute path.
The profile contains an explicit HTTPS IP address and absolute paths to three
distinct external files: the Client certificate chain, Client private key, and
exact trusted-server certificate.

The loader rejects oversized, malformed, duplicate, unknown, missing, or
incorrectly cased JSON content. It also rejects DNS names, absent ports,
non-HTTPS addresses, URI paths, queries, fragments, user information, relative
credential paths, directories, and missing or aliased credential files.

Increment 50C1 checks file custody only. It does not read or parse credential
bytes. Validation failures expose fixed reason codes without including profile
content, addresses, paths, or underlying exception text.

The profile file and every referenced credential remain external deployment
state. They must not be committed, included in source archives, printed, or
logged.

## Current scope

The package currently provides:

- distribution name `hase-client` and import namespace `hase`;
- an isolated and repeatable development dependency set;
- reproducible version-1 protobuf and gRPC bindings;
- byte-exact freshness and descriptor-level parity validation; and
- an immutable strict external Runtime Host profile model.

Credential characterization, offline PEM provisioning, mutual-TLS channels,
snapshots, Properties, Commands, and observations require later approved
increments.

