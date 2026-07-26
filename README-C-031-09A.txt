HASE Phase 7 C-031.09A - Explicit TLS client configuration

Complete replacement:
  tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\
    MutualTlsLoopbackGrpcHostIntegrationTests.cs

Correction:
  Replace HttpClientHandler certificate defaults with an explicit
  SocketsHttpHandler.SslOptions configuration.

Client TLS behavior:
  - TLS 1.3 explicitly enabled;
  - test client certificate explicitly supplied;
  - test server certificate explicitly accepted;
  - handler remains compatible with HTTP/2 gRPC.

No production-code changes are included.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete solution.
  3. Run the complete test suite.
  4. Expected total: 2,781 tests.
