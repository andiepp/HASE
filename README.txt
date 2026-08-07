HASE — Increment 47A — Serialized KEL-103 Health-Probe Primitive

Authoritative source baseline:
f31edf631d092036b50724d491a327142abeb3b9

Apply this overlay at the repository root so that the src and tests directories
merge with the existing directories.

This increment adds no periodic monitor and causes no background SCPI traffic.
It adds one fixed read-only health primitive using exactly one *IDN? query,
coordinates that primitive through the existing serialized connection slot,
projects failures with a fixed sanitized endpoint detail, and adds focused
automated tests.

Validation:
1. Open HASE.slnx in Visual Studio 2026.
2. Select Release.
3. Build the solution.
4. Run all automated tests.

Do not commit and do not begin physical validation until all automated tests
pass.
