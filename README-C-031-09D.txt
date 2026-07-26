HASE Phase 7 C-031.09D - Windows-compatible integration certificates

Complete replacement:
  tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\
    MutualTlsLoopbackGrpcHostIntegrationTests.cs

Correction:
  Replace the shared certificate helper with separate server and client
  certificate construction.

Server test certificate:
  - RSA 2048;
  - SHA-256 / PKCS#1 signature;
  - DigitalSignature and KeyEncipherment key usages;
  - Server Authentication EKU;
  - SAN localhost;
  - SAN 127.0.0.1.

Client test certificate:
  - RSA 2048;
  - SHA-256 / PKCS#1 signature;
  - DigitalSignature key usage;
  - Client Authentication EKU.

Reason:
  Windows Schannel can abort a TLS 1.2 server handshake when an RSA server
  certificate is not suitable for the negotiated server credential use. The
  previous integration certificate declared DigitalSignature only.

No HASE production-code or transport-policy changes are included.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete solution.
  3. Run only MutualTlsLoopbackGrpcHostIntegrationTests first.
  4. If it passes, run the complete test suite.
  5. Expected total: 2,781 tests.
