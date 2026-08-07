HASE Increment 49N - Contract Test Boundary Correction

Baseline
  Apply after the original Increment 49N overlay.

Replacement file
  tests/Hase.DesktopHost.Tests/Configuration/DesktopRuntimeHostRemoteDiagnosticsMigrationRestoreContractTests.cs

Correction
  Includes the parameter block closing parenthesis before asserting that the
  parameter block contains param(). No production source or behavior changes.

Validate in Visual Studio 2026
  1. Extract this ZIP at the repository root and replace the matching test file.
  2. Select Release configuration.
  3. Build the entire solution.
  4. Run all automated tests.
  5. Expected total: 5,688 passed.

No Runtime Host update, Client update, or physical validation is required.
Do not commit until all 5,688 tests pass.
