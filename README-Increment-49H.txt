HASE Increment 49H — Optional authenticated gRPC authorization composition

Authoritative baseline
----------------------
Commit: 59fb9f4cbf3e17becec1e94d30a2a207ce5378c1 (Increment 49G)
Configuration: Visual Studio 2026, Release, .NET 10
Baseline automated tests: 5,621 passed

Overlay contents
----------------
The ZIP contains complete replacement files rooted at the repository root.
Extract it over a clean checkout of the authoritative baseline.

Scope
-----
- Reconstruct the authenticated runtime-host client principal from the exact
  HASE claims projected into the active ASP.NET Core request.
- Reject missing, duplicate, empty, unauthenticated, or malformed claim data
  without including claim values in diagnostics.
- Optionally compose the principal provider, semantic permission mapper,
  immutable policy service, and authorization gate in mutual-TLS observation
  and diagnostic-projection hosting.
- Forward the optional policy through private-network deployment composition.
- Preserve the existing behavior when no authorization policy is supplied.

Deliberately excluded
---------------------
- Policy-file loading or policy administration.
- Installer or production configuration changes.
- Production authorization activation.
- Client changes.
- Deployment-sensitive values.

Release validation
------------------
1. Close running HASE applications that could retain build outputs.
2. Open HASE.slnx in Visual Studio 2026.
3. Select Release and build the entire solution.
4. Run all automated tests.

Expected result: 5,635 passed, 0 failed.

Runtime update and physical validation
--------------------------------------
No Client or Runtime Host update command is required. The new composition is
inactive unless a caller explicitly supplies an authorization policy, and this
increment does not activate one in production. No hardware or deployment
physical validation is required.

Stop point
----------
Do not commit until the complete Release build succeeds and all 5,635 automated
tests pass.
