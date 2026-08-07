HASE — Increment 47C — ADR-0047 Documentation and Closure

Authoritative source baseline:
cef2e75e94b0f924713bf2b205a562ea4e500ea6

Apply this overlay at the repository root so that the docs directory merges
with the existing directory.

Complete files:
- new ADR-0047 Passive SCPI Instrument Health Supervision;
- updated KEL-103 SCPI serial characterization report;
- updated Project Status;
- updated Roadmap.

Review the four documents, build HASE.slnx in Visual Studio 2026 Release, and
run all automated tests. The expected unchanged result is 5,497 tests passing.

No Runtime Host or Client update and no additional physical validation are
required. Do not commit until the documentation is accepted and all automated
tests pass.
