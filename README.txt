HASE Increment 48D - Structured SCPI Text Interpretation
=========================================================

Authoritative baseline:
  commit f4ab63cf2a25283f29a74b8849462adcbd006cf1
  Visual Studio 2026, Release, .NET 10
  5,522 automated tests passing

Overlay contents:
  src/Hase.DesktopHost/DesktopRuntimeByteInterpretationService.cs
  src/Hase.DesktopHost/ScpiTextDesktopRuntimeByteInterpreter.cs
  tests/Hase.DesktopHost.Tests/ScpiTextDesktopRuntimeByteInterpreterTests.cs

Behavior:
  - The existing Host structured-byte service recognizes the ScpiText family.
  - CR-terminated Query and Command requests and LF-terminated responses are
    projected as read-only structured fields.
  - Missing, malformed, trailing, non-ASCII, and truncated snapshots are
    reported safely without affecting runtime behavior.
  - The generic Host diagnostics UI requires no modification.

Automated validation:
  1. Extract this ZIP over the repository root.
  2. Build the complete solution in Visual Studio 2026, Release.
  3. Run all automated tests.
  4. Expected total: 5,533 passing tests.

Deployment before physical validation:
  Stop the Desktop Runtime Host, then run:

    cd H:\Development
    & .\tools\Deployment\Update-HaseDesktopRuntimeHost.ps1

No Client update is required for Increment 48D.
