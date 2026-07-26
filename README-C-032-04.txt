HASE - Phase 7 C-032.04
Reject an authoritative Property RPC without a client certificate

Baseline
--------
C-032.01 through C-032.03 are applied locally.
2,787 automated tests pass.
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
3. Expected result: 2,788 tests pass.
4. Do not commit yet. Report the build and test result first.

Scope
-----
This increment adds one real HTTPS/HTTP/2 gRPC integration test. It proves:

- the client presents no certificate;
- Kestrel rejects the request at the mutual-TLS transport boundary;
- the gRPC client observes StatusCode.Unavailable, matching the established
  C-031 platform behavior;
- IRuntimeHostPropertyService.ReadAsync is never executed;
- the cached Property path is never executed.

No production file is changed by C-032.04.
