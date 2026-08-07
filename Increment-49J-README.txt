HASE Increment 49J replacement-file overlay
===========================================

Authoritative baseline
----------------------
Commit: 4a23043a0d6079dfdf2aa11d2ef11ef428edc93a
Configuration: Release
Framework: .NET 10
Baseline automated tests: 5,660 passing
Expected automated tests after overlay: 5,666 passing

Scope
-----
Adds production authorization-policy composition without provisioning any
policy contents. The Desktop Runtime Host installation profile can reference
one distinct, fully qualified authorization-policy file. Remote diagnostics
require that explicit reference. Production startup strictly loads the bounded
policy before listening, attaches the approved diagnostic projection only when
enabled, and supplies both the projection and immutable authorization policy
to the existing private-network deployment composition.

Existing installations remain unchanged while remote diagnostics are disabled
and no policy reference is present. No principal identities, permission grants,
addresses, certificate values, or other deployment-sensitive values are
included in this overlay.

Apply and validate
------------------
1. Extract the ZIP at the repository root, preserving paths.
2. Open the solution in Visual Studio 2026.
3. Select Release configuration and build the entire solution.
4. Run all automated tests.
5. Confirm exactly 5,666 tests pass with no failures.

No Runtime Host or Client update command and no physical validation are
required for Increment 49J. Policy provisioning and installed-profile
migration remain deferred. Do not commit until the Release build and complete
automated test run succeed.
