HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 8 - Secure host composition

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increments C-031.01 through C-031.07
  2,773 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production file:
  src\Hase.Runtime.Remote.Grpc.Hosting\MutualTlsLoopbackGrpcHostFactory.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\MutualTlsLoopbackGrpcHostFactoryTests.cs

Purpose:
  Compose the validated C-031 transport and authentication components into one
  real ASP.NET Core gRPC host boundary without changing the existing verified
  plaintext LoopbackGrpcHostFactory.

Host behavior:
  - retains the validated LoopbackGrpcBinding restriction;
  - configures Kestrel for HTTP/2 only;
  - configures HTTPS with TLS 1.3;
  - requires a client certificate during the TLS handshake;
  - registers the C-030 certificate-authentication service;
  - registers the C-031 certificate, request, and HttpContext adapters;
  - installs RuntimeHostMutualTlsAuthenticationMiddleware before gRPC mapping;
  - exposes the existing RuntimeHostRemoteApiService snapshot operation.

This increment deliberately does not:
  - weaken or alter the existing plaintext loopback host;
  - enable non-loopback binding;
  - add Property, Command, or Observation overloads;
  - add a real TLS process integration test;
  - introduce authorization, revocation, rotation, logging, or audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that seven new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
