HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 7 - Mutual-TLS authentication middleware

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increments C-031.01 through C-031.06
  2,763 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsHttpContextItems.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsAuthenticationMiddleware.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostMutualTlsAuthenticationMiddlewareTests.cs

Purpose:
  Enforce certificate-backed HASE client authentication at the ASP.NET Core
  middleware boundary before any gRPC endpoint executes.

Behavior:
  - retrieves the client certificate from the current HTTPS connection;
  - authenticates it through the complete C-030/C-031 request pipeline;
  - projects HttpContext.User only after successful authentication;
  - invokes the next pipeline component only after acceptance;
  - returns HTTP 401 for rejected authentication;
  - stores the complete authentication result in HttpContext.Items;
  - uses TimeProvider.GetUtcNow for deterministic UTC authentication time.

This increment deliberately does not:
  - register the middleware in runtime-host startup;
  - wire the Kestrel HTTPS listener;
  - change any gRPC service implementation;
  - introduce authorization, revocation, rotation, logging, or audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that ten new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
