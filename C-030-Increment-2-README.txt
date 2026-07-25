HASE Phase 7 C-030 - Mutual TLS Authentication Foundation
Increment 2 - Credential enrollment foundation

Baseline:
  Commit f943dc741292945dae45483783c8293341a2a8a3
  2,637 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCredentialIdentity.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCredentialEnrollment.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\IRuntimeHostClientCredentialEnrollmentRegistry.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCredentialEnrollmentRegistry.cs

New test files:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientCredentialIdentityTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientCredentialEnrollmentTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientCredentialEnrollmentRegistryTests.cs

Validation:
  1. Build the complete .NET solution.
  2. Run the complete automated test suite.
  3. Report the resulting test count.

This increment does not configure certificates, TLS, Kestrel, gRPC
authentication, non-loopback listeners, file-based enrollment, rotation,
revocation, or audit behavior.
