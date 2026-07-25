HASE Phase 7 C-030 - Mutual TLS Authentication Foundation
Increment 4 - X.509 credential identity extraction

Baseline:
  Commit 64580b5bc37375095164b393aaa2cff1b9f0a83d
  2,670 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\IRuntimeHostX509ClientCredentialIdentityExtractor.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostX509ClientCredentialIdentityExtractor.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostX509ClientCredentialIdentityExtractorTests.cs

Credential identity rule:
  authentication mechanism = mutual-tls
  credential identifier     = x509-sha256:<lowercase SHA-256 DER certificate hash>

The extractor accepts an already validated certificate. It intentionally does
not validate certificate chains, validity periods, key usage, enhanced key
usage, revocation, enrollment, or authorization.

Validation:
  1. Build the complete .NET solution.
  2. Run the complete automated test suite.
  3. Report the resulting test count.

This increment does not configure TLS, Kestrel, gRPC authentication,
non-loopback listeners, certificate trust, rotation, revocation, or audit
behavior.
