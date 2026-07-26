HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 6 - Request authentication integration

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increments C-031.01 through C-031.05
  2,754 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production file:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostMutualTlsRequestAuthenticator.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostMutualTlsRequestAuthenticatorTests.cs

Purpose:
  Combine C-031.03 client-certificate authentication with C-031.05
  HttpContext identity projection at one request-level boundary.

Behavior:
  - requires a non-null HttpContext;
  - authenticates the presented certificate at an explicit UTC time;
  - projects HttpContext.User only after successful authentication;
  - leaves HttpContext.User unchanged when authentication fails;
  - preserves detailed invalid, untrusted, and unknown-credential failures;
  - fails closed on inconsistent accepted results.

This increment deliberately does not:
  - wire Kestrel;
  - register middleware;
  - open or move a listener;
  - change any gRPC service;
  - introduce authorization, revocation, rotation, logging, or audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that nine new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
