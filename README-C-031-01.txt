HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 1 - Runtime-host mutual-TLS configuration

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Latest committed capability: C-030 certificate authentication pipeline

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production file:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsOptions.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostMutualTlsOptionsTests.cs

Purpose:
  Introduce one immutable, fail-closed configuration object for enabling or
  disabling the runtime-host mutual-TLS listener.

Invariants:
  - enabled mutual TLS requires a server certificate;
  - enabled mutual TLS always requires a client certificate;
  - disabled mutual TLS carries no server certificate;
  - disabled mutual TLS does not request a client certificate.

This increment deliberately does not:
  - configure Kestrel;
  - open a non-loopback listener;
  - perform a TLS handshake;
  - duplicate the C-030 certificate-authentication pipeline;
  - change any gRPC service behavior;
  - introduce authorization, custom trust anchors, revocation, rotation, or
    audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that seven new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
