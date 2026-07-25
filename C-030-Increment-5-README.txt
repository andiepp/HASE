HASE Phase 7 C-030 - Mutual TLS Authentication Foundation
Increment 5 - Local client-certificate validation policy

Baseline:
  Commit deeb5cf81dc075c323c772df9be0964ee64d2609
  2,675 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCertificateValidationFailureReason.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCertificateValidationResult.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\IRuntimeHostClientCertificateValidator.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostClientCertificateValidator.cs

New test files:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientCertificateValidationFailureReasonTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientCertificateValidationResultTests.cs
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostClientCertificateValidatorTests.cs

Local validation rules:
  - certificate must be present;
  - explicit evaluation time must use UTC;
  - NotBefore and NotAfter are enforced inclusively;
  - when an Enhanced Key Usage extension is present, it must contain the
    Client Authentication OID 1.3.6.1.5.5.7.3.2;
  - malformed certificate metadata fails closed.

This increment intentionally does not perform:
  - certificate-chain trust validation;
  - revocation checking;
  - enrollment lookup;
  - credential identity extraction;
  - authentication or authorization;
  - TLS, Kestrel, or gRPC integration.

Validation:
  1. Build the complete .NET solution.
  2. Run the complete automated test suite.
  3. Report the resulting test count.
