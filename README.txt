HASE Increment 49O4 — Supervised Authorization-Policy Substitution

Add these complete files to the repository:

  tools/Deployment/Substitute-HaseDesktopRuntimeHostRemoteDiagnosticsAuthorizationPolicy.ps1
  tools/Deployment/Restore-HaseDesktopRuntimeHostRemoteDiagnosticsAuthorizationPolicy.ps1
  tests/Hase.DesktopHost.Tests/Configuration/DesktopRuntimeHostRemoteDiagnosticsAuthorizationPolicySubstitutionContractTests.cs

Scope:

  The substitution command accepts an externally prepared policy only while
  the Runtime Host is stopped. It validates that exactly one
  diagnostics.subscribe grant was removed and every other grant is preserved,
  then replaces the installed policy atomically while retaining the authorized
  bytes. The parameterless restore command atomically restores those exact
  authorized bytes and retains the denied policy for audit. Both commands fail
  closed on dirty or unsupported state and withhold sensitive values.

Validation:

  1. Build the entire solution with Visual Studio 2026 in Release.
  2. Run all automated tests. Expected total: 5,726.
  3. Report the build and test result before running either command.

No Client or Runtime Host update command and no physical validation are
required before the automated checkpoint. Do not commit yet.
