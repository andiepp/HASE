HASE - Phase 7 C-032.03
Reject an unenrolled authoritative Property RPC before service execution

Baseline
--------
C-032.01 and C-032.02 are applied locally.
2,786 automated tests pass.
The C-032 changes are intentionally not committed yet.

Files
-----
Replace:
tests\Hase.Runtime.Remote.Grpc.Hosting.Tests\
MutualTlsLoopbackGrpcHostPropertyIntegrationTests.cs

Apply
-----
Extract this archive directly into:

H:\Development

Allow the tests directory in the archive to merge with the existing:

H:\Development\tests

Verification
------------
1. Build HASE.slnx.
2. Run the complete automated test suite.
3. Expected result: 2,787 tests pass.
4. Do not commit yet. Report the build and test result first.

Scope
-----
This increment adds one real HTTPS/HTTP/2 gRPC integration test. It proves:

- a CA-issued client certificate completes the mutual-TLS transport boundary;
- the presented certificate is structurally valid and explicitly trusted by
  the isolated test authentication pipeline;
- the credential is not enrolled as a HASE client principal;
- ReadAuthoritativeProperty is rejected with gRPC StatusCode.Unauthenticated;
- IRuntimeHostPropertyService.ReadAsync is never executed;
- the cached Property path is never executed.

No production file is changed by C-032.03.
