HASE — Increment 47B — Passive Idle KEL-103 Health Monitor Lifecycle

Authoritative source baseline:
b86127704f6861c8de043dbc8409a60fe0f9f559

Apply this overlay at the repository root so the src and tests directories
merge with the existing directories.

This increment:
- starts exactly one passive monitor per supervised KEL-103 attachment;
- waits five seconds before the first probe;
- probes only while the endpoint is Ready;
- waits a complete interval after each completed probe;
- uses the serialized Increment 47A *IDN? health primitive;
- lets existing recovery supervision own reconnection and authoritative
  synchronization;
- stops the health monitor before recovery supervision and attachment disposal;
- prevents orderly monitor cancellation from being projected as a
  communication fault;
- adds six focused automated tests.

Validation:
1. Open HASE.slnx in Visual Studio 2026.
2. Select Release.
3. Build the solution.
4. Run all automated tests.

Do not publish the Runtime Host, begin physical validation, or commit until all
automated tests pass.
