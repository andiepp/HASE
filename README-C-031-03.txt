HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 3 - Client-certificate callback adapter

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increments C-031.01 and C-031.02
  2,728 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsClientCertificateAuthenticationResult.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsClientCertificateAuthenticator.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostMutualTlsClientCertificateAuthenticatorTests.cs

Purpose:
  Adapt the complete C-030 certificate-authentication pipeline to the
  accept/reject decision required by a future Kestrel TLS callback while
  retaining the authenticated RuntimeHostClientPrincipal for projection into
  HttpContext.User.

Behavior:
  - delegates all certificate validation, trust validation, identity
    extraction, and enrollment to C-030;
  - accepts only an authenticated C-030 result;
  - rejects invalid, untrusted, and unknown credentials;
  - preserves detailed failure reasons;
  - requires an explicit UTC authentication timestamp;
  - fails closed on an invalid service response.

This increment deliberately does not:
  - modify Kestrel callback wiring;
  - mutate HttpContext.User;
  - open or move a listener;
  - change any gRPC service behavior;
  - introduce authorization, revocation, rotation, or audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that nine new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
