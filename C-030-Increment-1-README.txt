HASE Phase 7 C-030 - Mutual TLS Authentication Foundation
Increment 1 - Transport-independent authentication values

Baseline:
  Commit cc985d71b04b5be8903fc9b4343b465c55e143f4

Extract this archive into:
  HASE\Development

Files:
  New:
    src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientPrincipalId.cs
    src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCredentialId.cs
    src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostAuthenticationMechanism.cs
    tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostAuthenticationValueTests.cs

  Replace:
    src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientPrincipal.cs
    tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientPrincipalTests.cs

Validation:
  1. Build the complete .NET solution.
  2. Run the complete automated test suite.
  3. Report the resulting test count.

This increment does not configure TLS, certificates, Kestrel, non-loopback
listeners, credential enrollment, or certificate-to-principal mapping.
