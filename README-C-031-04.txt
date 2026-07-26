HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 4 - ClaimsPrincipal projection

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increments C-031.01 through C-031.03
  2,737 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientClaimTypes.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsAuthenticationDefaults.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClaimsPrincipalFactory.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClaimsPrincipalFactoryTests.cs

Purpose:
  Project an authenticated RuntimeHostClientPrincipal into one authenticated
  ClaimsPrincipal suitable for assignment to HttpContext.User.

Projection:
  - PrincipalId
  - CredentialId
  - AuthenticationMechanism
  - AuthenticatedAtUtc
  - TrustPolicyId

Identity behavior:
  - authentication scheme: HASE.MutualTls;
  - PrincipalId is the ClaimsIdentity name claim;
  - no role claims;
  - no permissions or authorization decisions;
  - exactly one identity and five HASE claims.

This increment deliberately does not:
  - mutate HttpContext.User;
  - wire Kestrel or middleware;
  - change any gRPC service;
  - introduce roles, permissions, policy evaluation, or endpoint
    authorization;
  - introduce logging, revocation, rotation, or audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that nine new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
