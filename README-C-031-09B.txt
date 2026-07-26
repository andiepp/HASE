HASE Phase 7 C-031.09B - Forced client-certificate selection

Complete replacement:
  tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\
    MutualTlsLoopbackGrpcHostIntegrationTests.cs

Correction:
  Add SslClientAuthenticationOptions.LocalCertificateSelectionCallback and
  explicitly return the integration client certificate.

Reason:
  On Windows, Schannel may filter ClientCertificates against the acceptable
  issuer list supplied by the server. The self-signed integration certificate
  can therefore remain unselected even when present in ClientCertificates.

No production-code changes are included.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete solution.
  3. Run the complete test suite.
  4. Expected total: 2,781 tests.

If the test still fails, copy the full exception text including all inner
exceptions from the Test Detail Summary.
