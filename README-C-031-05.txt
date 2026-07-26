HASE Phase 7 C-031 - Mutual TLS Runtime Host Integration
Increment 5 - HTTP-context identity projection

Baseline:
  Commit 6200b87c2ca5a4615cc0f7453c12062a77288a0b
  Local validated increments C-031.01 through C-031.04
  2,746 automated tests pass

Extract this archive into:
  HASE\Development

All files in this archive are new.

New production file:
  src\Hase.Runtime.Remote.Grpc.Adapter\RuntimeHostHttpContextIdentityProjector.cs

New test file:
  tests\Hase.Runtime.Remote.Grpc.Adapter.Tests\RuntimeHostHttpContextIdentityProjectorTests.cs

Purpose:
  Assign the ClaimsPrincipal produced by C-031.04 to HttpContext.User for one
  authenticated runtime-host request.

Behavior:
  - requires a non-null HttpContext;
  - requires a non-null authenticated RuntimeHostClientPrincipal;
  - creates the established HASE mutual-TLS ClaimsPrincipal;
  - replaces the existing anonymous user;
  - does not modify the request method, path, or unrelated context state.

This increment deliberately does not:
  - wire Kestrel;
  - register middleware;
  - execute certificate authentication;
  - alter any gRPC service;
  - introduce roles, permissions, authorization, revocation, rotation, or
    audit behavior.

Validation:
  1. Extract into HASE\Development.
  2. Build the complete .NET solution in Visual Studio.
  3. Run the complete automated test suite.
  4. Confirm that eight new tests pass.
  5. Report the resulting total test count.

Do not commit until the build and complete test suite pass.
