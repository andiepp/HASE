HASE Increment 49A - Diagnostic Projection Contract and Policy Foundation
=========================================================================

Authoritative baseline:
  commit 088bc65bc1d584ae9aa210757eadfd68f57fdc82
  Visual Studio 2026, Release, .NET 10
  5,533 automated tests passing

Overlay contents:
  src/Hase.Runtime.Northbound/RuntimeHostDiagnosticProjectionPolicy.cs
  src/Hase.Runtime.Northbound/RuntimeHostDiagnosticProjector.cs
  src/Hase.Runtime.Northbound/RuntimeHostProjectedDiagnosticByteSnapshot.cs
  src/Hase.Runtime.Northbound/RuntimeHostProjectedDiagnosticRecord.cs
  tests/Hase.Runtime.Tests.Northbound/RuntimeHostDiagnosticProjectionContractTests.cs

Behavior:
  - Remote Runtime Host diagnostic projection is disabled by default.
  - Enabling without an explicit level permits Operational records only.
  - The remote ceiling cannot exceed the Host capture level.
  - Projected records own immutable structure, safe allowlisted details, and
    optional exact bounded byte snapshots.
  - No production Host, gRPC, Client, configuration, or UI path is activated.

Automated validation:
  1. Extract this ZIP over the repository root.
  2. Build the complete solution in Visual Studio 2026, Release.
  3. Run all automated tests.
  4. Expected total: 5,545 passing tests.

No Runtime Host or Client update and no physical validation are required for
this production-inactive Increment 49A foundation.
