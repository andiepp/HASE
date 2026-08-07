HASE Increment 48B - SCPI Diagnostic Disclosure and Record Mapping
=================================================================

Authoritative baseline:
  commit 5caec8a158f0bef6e9a994d20081247c5bbeaee8
  Visual Studio 2026, Release, .NET 10
  5,508 automated tests passing

Overlay contents:
  src/Hase.Scpi.Kel103.Hosting/Kel103ScpiDiagnosticObserver.cs
  tests/Hase.Scpi.Kel103.Hosting.Tests/Kel103ScpiDiagnosticObserverTests.cs

Behavior:
  - Operational capture publishes no SCPI exchange or byte records.
  - Protocol capture publishes correlated, payload-free exchange metadata.
  - Bytes capture additionally publishes bounded exact-byte snapshots through
    the existing RuntimeTransportByteDiagnosticPublisher.
  - Sanitized failure classification and uncertain command outcome are
    preserved.
  - Production KEL-103 session composition is intentionally unchanged.

Validation:
  1. Extract this ZIP over the repository root.
  2. Build the complete solution in Visual Studio 2026, Release.
  3. Run all automated tests.
  4. Expected total: 5,520 passing tests.

No Runtime Host or Client update and no physical validation are required for
this production-inactive increment.
