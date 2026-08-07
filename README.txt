HASE — Increment 48A — Transport-Independent SCPI Diagnostic Observation

Authoritative source baseline:
b947d7668f42e131d8ba72eb171920727997781c

Apply this overlay at the repository root so the src and tests directories
merge with the existing directories.

This increment adds an optional dependency-free SCPI diagnostic observer,
immutable copied byte observations, exchange start/completion/failure records,
safe outcome and failure classification, and ScpiTextSession integration.

The existing ScpiTextSession constructor remains diagnostics-disabled.
Production KEL-103 composition is unchanged, so this increment creates no Host
or Client diagnostic records and requires no physical validation.

Validation:
1. Open HASE.slnx in Visual Studio 2026.
2. Select Release.
3. Build the solution.
4. Run all automated tests.

Eleven focused tests are added. The expected total is 5,508 passing tests.
Do not commit until the build and all automated tests pass.
