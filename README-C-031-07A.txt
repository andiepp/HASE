HASE Phase 7 C-031.07A - Middleware test build fix

Replace:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostMutualTlsAuthenticationMiddlewareTests.cs

Correction:
  RuntimeHostClientCertificateValidationFailureReason.MissingCertificate
  was an incorrect enum member name.

  The existing C-030 enum defines:
  RuntimeHostClientCertificateValidationFailureReason.CertificateMissing

No production code changes are included.

Validation:
  1. Extract into HASE\Development.
  2. Rebuild the complete solution.
  3. Run the complete test suite.
  4. Expected total: 2,773 tests.
