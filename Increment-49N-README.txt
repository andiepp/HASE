HASE Increment 49N - Supervised Runtime Host Remote-Diagnostics Migration Rollback

Authoritative baseline
  Commit: 39f2bedba69a949c57bca092c9474c12af0edbe2
  Configuration: Release
  Framework: .NET 10
  Baseline automated tests: 5,680 passed

Overlay contents
  tools/Deployment/Restore-HaseDesktopRuntimeHostRemoteDiagnosticsMigration.ps1
  tests/Hase.DesktopHost.Tests/Configuration/DesktopRuntimeHostRemoteDiagnosticsMigrationRestoreContractTests.cs

Apply
  Extract this ZIP at the repository root.

Validate in Visual Studio 2026
  1. Select Release configuration.
  2. Build the entire solution.
  3. Run all automated tests.
  4. Expected total: 5,688 passed.

Increment boundary
  Do not run the restore script as part of 49N source validation.
  No Runtime Host update, Client update, or physical validation is required.
  Do not commit until the Release build and all 5,688 tests pass.

Rollback behavior delivered for later supervised use
  - Requires the stopped, completed, migrated guided installation.
  - Validates fixed profile paths, migrated state, original backup, and policy.
  - Restores the exact original profile bytes with atomic replacement.
  - Retains the migrated profile as a stable recovery backup.
  - Retains the authorization policy unchanged and inactive.
  - Restores the migrated state if post-replacement verification fails.
  - Emits only sanitized custody-state diagnostics.
