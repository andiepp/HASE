HASE Increment 49F replacement-file overlay
===========================================

Authoritative baseline
----------------------
Commit: 7dbe6fa71f4f3e29d3f0657fcaaf29e524447180
Configuration: Release
Framework: .NET 10
Baseline automated tests: 5,601 passing
Expected automated tests after overlay: 5,611 passing

Scope
-----
Adds a late-bound diagnostic projection mechanism to the Desktop diagnostic
session. One stable RuntimeDiagnosticPublisher initially targets only the local
bounded collector. Explicit attachment with an authoritative Runtime Host
identity atomically redirects future publication through the projection
service while preserving local retention and publisher identity. No retained
history is replayed.

Session disposal redirects publication back to the local collector before
ending projection subscriptions. Attachment is single-use, validates the
remote ceiling against local capture, and remains production-inactive because
the production backend does not call it.

Apply and validate
------------------
1. Extract the ZIP at the repository root, preserving paths.
2. Open the solution in Visual Studio 2026.
3. Select Release configuration and build the entire solution.
4. Run all automated tests.
5. Confirm exactly 5,611 tests pass with no failures.

No Runtime Host or Client update command and no physical validation are
required for Increment 49F. Do not commit until the Release build and complete
automated test run succeed.
