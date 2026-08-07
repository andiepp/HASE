HASE Increment 49C replacement-file overlay
===========================================

Authoritative baseline
----------------------
Commit: 887b54777662c6350051cebe76e90f1d9c461df1
Configuration: Release
Framework: .NET 10
Baseline automated tests: 5,557 passing
Expected automated tests after overlay: 5,584 passing

Scope
-----
Adds the version 1 protobuf representation for already-sanitized Runtime Host
diagnostic projection observations and a pure Adapter mapper. The contract
preserves subscription-local and source sequences, UTC timestamp, stable enum
values, optional operational context, duration, sanitized details, and the
bounded projected byte snapshot.

This overlay does not add an RPC, server streaming, Host composition,
authorization, configuration, enablement, Client transport, or Client UI.
Diagnostic projection therefore remains production-inactive.

Apply
-----
Extract the ZIP at the repository root, preserving paths and replacing the
existing protobuf contract file.

Validation
----------
1. Open the solution in Visual Studio 2026.
2. Select Release configuration.
3. Build the entire solution.
4. Run all automated tests.
5. Confirm exactly 5,584 tests pass with no failures.

No Runtime Host or Client update command and no physical validation are
required for Increment 49C. Do not commit until the Release build and complete
automated test run succeed.
