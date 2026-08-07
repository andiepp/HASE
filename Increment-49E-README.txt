HASE Increment 49E replacement-file overlay
===========================================

Authoritative baseline
----------------------
Commit: a0ffa98a365a5ae4b8aa1264330ae3065c5f714e
Configuration: Release
Framework: .NET 10
Baseline automated tests: 5,595 passing
Expected automated tests after overlay: 5,601 passing

Scope
-----
Adds explicit optional diagnostic projection forwarding and dependency
registration through loopback, mutual-TLS loopback, private-network, and
private-network deployment hosting layers. When supplied, hosting registers
the exact externally owned projection service and its gRPC mapper. Existing
overloads continue to supply no diagnostic service.

The production Desktop Runtime Host still supplies no diagnostic projection
service. This overlay does not change diagnostic publisher construction,
Runtime Host identity resolution, configuration, authorization policy,
credential provisioning, Client transport, or Client UI. Remote diagnostics
therefore remain unavailable in production.

Apply and validate
------------------
1. Extract the ZIP at the repository root, preserving paths.
2. Open the solution in Visual Studio 2026.
3. Select Release configuration and build the entire solution.
4. Run all automated tests.
5. Confirm exactly 5,601 tests pass with no failures.

No Runtime Host or Client update command and no physical validation are
required for Increment 49E. Do not commit until the Release build and complete
automated test run succeed.
