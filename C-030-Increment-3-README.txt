HASE Phase 7 C-030 - Mutual TLS Authentication Foundation
Increment 3 - Transport-independent authentication resolver

Baseline:
  Commit 6fecdc8a84ca9f1fbca3583b9ae30a5d3ad81f4c
  2,656 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostAuthenticationFailureReason.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostAuthenticationResult.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\IRuntimeHostClientAuthenticationService.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientAuthenticationService.cs

New test files:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostAuthenticationFailureReasonTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostAuthenticationResultTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientAuthenticationServiceTests.cs

Validation:
  1. Build the complete .NET solution.
  2. Run the complete automated test suite.
  3. Report the resulting test count.

Unknown validated credentials produce an explicit fail-closed authentication
result. Malformed identities, non-UTC timestamps, and enrollment-registry
contract violations remain programming or configuration exceptions.

This increment does not configure certificates, TLS, Kestrel, gRPC
authentication, non-loopback listeners, file-based enrollment, rotation,
revocation, or audit behavior.
