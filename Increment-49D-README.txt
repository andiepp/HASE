HASE Increment 49D replacement-file overlay
===========================================

Authoritative baseline
----------------------
Commit: 6ff2c4b565d1d903a5bc157a188359ba7a87f53f
Configuration: Release
Framework: .NET 10
Baseline automated tests: 5,584 passing
Expected automated tests after overlay: 5,595 passing

Scope
-----
Adds the authorized ObserveDiagnostics server-streaming v1 RPC adapter. The
adapter opens a live-only bounded diagnostic projection subscription, maps and
writes records sequentially, links request and Host-shutdown cancellation,
disposes every subscription, and translates a terminal subscriber gap to the
gRPC DataLoss status. A dedicated diagnostics.subscribe permission protects
the semantic remote operation.

This overlay does not compose the diagnostic projection service into a
production Host, replace the production diagnostic sink, enable projection,
add configuration values, register new Host dependencies, or add Client
transport or UI behavior. Diagnostic streaming therefore remains unavailable
in production.

Apply and validate
------------------
1. Extract the ZIP at the repository root, preserving paths.
2. Open the solution in Visual Studio 2026.
3. Select Release configuration and build the entire solution.
4. Run all automated tests.
5. Confirm exactly 5,595 tests pass with no failures.

No Runtime Host or Client update command and no physical validation are
required for Increment 49D. Do not commit until the Release build and complete
automated test run succeed.
