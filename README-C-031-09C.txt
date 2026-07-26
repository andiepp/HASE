HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 9C - TLS 1.2 or newer policy correction

Approved policy:
  - TLS 1.2 is the minimum permitted protocol.
  - TLS 1.3 is also permitted and is negotiated where supported.
  - TLS 1.0 and TLS 1.1 remain prohibited.

Complete replacements:
  src\Hase.Runtime.Remote.Grpc.Adapter\
    RuntimeHostMutualTlsKestrelConfigurationFactory.cs

  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\
    RuntimeHostMutualTlsKestrelConfigurationFactoryTests.cs

  tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\
    MutualTlsLoopbackGrpcHostIntegrationTests.cs

Reason:
  Windows 10 Schannel does not provide TLS 1.3 support. A TLS 1.3-only policy
  therefore prevents the HASE runtime host and client from negotiating a common
  protocol on the supported Windows 10 development/runtime platform.

Behavior after correction:
  - Windows 10 negotiates TLS 1.2.
  - Platforms supporting TLS 1.3 may negotiate TLS 1.3.
  - no fallback to TLS 1.0 or TLS 1.1;
  - HTTP/2 and required client-certificate behavior remain unchanged;
  - the complete C-030 authentication pipeline remains authoritative.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete solution.
  3. Run the complete automated test suite.
  4. Expected total: 2,781 tests.

Do not commit until the build and complete test suite pass.
