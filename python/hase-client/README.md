# HASE Python Client

This directory contains the asyncio-native Python Client for the HASE Runtime
Host. Increment 50B1 establishes only the isolated package and test toolchain.
It does not yet contain generated protobuf contracts, open a Runtime Host
connection, load credentials, or invoke hardware.

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

## Current scope

Increment 50B1 provides:

- distribution name `hase-client`;
- import namespace `hase`;
- an isolated and repeatable development dependency set; and
- one package identity test.

The Runtime Host contract, secure sessions, snapshots, Properties, Commands,
observations, deployment profiles, and certificate handling are added only by
later approved increments.

