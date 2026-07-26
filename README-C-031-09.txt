HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 9 - Authenticated HTTPS/gRPC integration

Baseline:
  Local validated increments C-031.01 through C-031.08
  2,780 automated tests pass

Extract this archive into:
  HASE\Development

Complete replacement:
  src\Hase.Runtime.Remote.Grpc.Hosting\MutualTlsLoopbackGrpcHostFactory.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\MutualTlsLoopbackGrpcHostIntegrationTests.cs

Purpose:
  Verify one real HTTPS/HTTP2 gRPC snapshot request through the complete
  C-030/C-031 runtime path.

Verified path:
  - real Kestrel HTTPS listener;
  - HTTP/2 only;
  - server certificate presented;
  - client certificate required by Kestrel;
  - HASE local certificate validation;
  - HASE trust validation;
  - X.509 credential identity extraction;
  - enrollment-backed authentication;
  - RuntimeHostClientPrincipal creation;
  - ClaimsPrincipal projection into HttpContext.User;
  - middleware acceptance before gRPC;
  - snapshot RPC reaches the northbound provider.

Kestrel certificate behavior:
  The TLS layer requires presentation of a client certificate. Platform
  certificate acceptance is deferred to the C-030 pipeline through the Kestrel
  callback. This allows HASE to own deterministic trust and enrollment policy.
  Rejected HASE authentication still stops before the gRPC service executes.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution.
  3. Run the complete automated test suite.
  4. Confirm that one new integration test passes.
  5. Expected total: 2,781 tests.

Do not commit until the build and complete test suite pass.
