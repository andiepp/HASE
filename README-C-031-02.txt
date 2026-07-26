HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 2 - Kestrel mutual-TLS policy mapping

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increment C-031.01
  2,720 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production files:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsKestrelConfiguration.cs
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsKestrelConfigurationFactory.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostMutualTlsKestrelConfigurationFactoryTests.cs

Purpose:
  Deterministically map an enabled RuntimeHostMutualTlsOptions instance to the
  Kestrel transport policy required by ADR-0031 and C-031.

Resulting policy:
  - HTTP/2 only;
  - HTTPS required;
  - TLS 1.3 only;
  - server certificate preserved from configuration;
  - client certificate required during the TLS handshake;
  - disabled configuration rejected.

This increment deliberately does not:
  - modify runtime-host startup;
  - open or move a listener;
  - provide a certificate-validation callback;
  - execute the C-030 authentication pipeline;
  - change any gRPC service behavior;
  - introduce authorization, trust-store reload, revocation, rotation, or
    audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that eight new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
