HASE Phase 7 C-030 - Mutual TLS Authentication Foundation
Increment 6 - System certificate-chain trust adapter

Baseline:
  Commit bb181a4d25790bf769fb05961321b778d843a3a6
  2,690 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostCertificateTrustFailureReason.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostCertificateTrustValidationResult.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\IRuntimeHostCertificateTrustEvaluator.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostSystemCertificateTrustEvaluator.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\IRuntimeHostCertificateTrustValidator.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostCertificateTrustValidator.cs

New test files:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostCertificateTrustValidationResultTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostCertificateTrustValidatorTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostSystemCertificateTrustEvaluatorTests.cs

System trust policy:
  - X509ChainTrustMode.System
  - explicit UTC verification time
  - X509VerificationFlags.NoFlag
  - revocation mode NoCheck for this increment
  - certificate downloads disabled
  - chain must build to a platform-trusted root

The evaluator remains inside the X.509 adapter boundary. X509ChainStatus is not
exposed to authentication, authorization, or northbound services.

Validation:
  1. Build the complete .NET solution.
  2. Run the complete automated test suite.
  3. Report the resulting test count.

This increment does not configure custom trust anchors, revocation, TLS,
Kestrel, gRPC authentication, non-loopback listeners, credential enrollment,
rotation, or audit behavior.
