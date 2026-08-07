HASE Increment 48C - Production KEL-103 SCPI Diagnostic Composition
==================================================================

Authoritative baseline:
  commit d866554d0c2facd3b3886ded05371026c177eb7a
  Visual Studio 2026, Release, .NET 10
  5,520 automated tests passing

Overlay contents:
  src/Hase.Scpi.Kel103.Hosting/Kel103OperationalConnectionFactory.cs
  tests/Hase.Scpi.Kel103.Hosting.Tests/Kel103SynchronizationDiagnosticTests.cs
  tests/Hase.Scpi.Kel103.Hosting.Tests/Kel103ScpiProductionDiagnosticCompositionTests.cs

Behavior:
  - Every production KEL-103 SCPI session receives one runtime diagnostic
    observer scoped to the authoritative endpoint identity.
  - Initial synchronization, operations, passive health, and recovered sessions
    use the same established Protocol and Bytes disclosure policy.
  - SCPI framing, timeout, serialization, mutation, and recovery behavior are
    unchanged.

Automated validation:
  1. Extract this ZIP over the repository root.
  2. Build the complete solution in Visual Studio 2026, Release.
  3. Run all automated tests.
  4. Expected total: 5,522 passing tests.

Deployment before physical validation:
  Stop the Desktop Runtime Host, then run:

    cd H:\Development
    & .\tools\Deployment\Update-HaseDesktopRuntimeHost.ps1

No Client update is required for Increment 48C.
