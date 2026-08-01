HASE - ADR-0043 Increment 43D2
Independent Runtime-Host Session Controller Core

Baseline commit: 8893378757af7c263c10851b00c865c3241e116c
Baseline tests : 4,233 passing
Expected tests : 4,240 passing

Extract this overlay into H:\Development with Visual Studio closed, reopen
HASE.slnx, build Release, and run all tests. Report the build result and exact
test count before committing.

This increment adds only the transport-independent per-profile controller,
its factory/controller contracts, and focused tests. It does not change the
registry reader, gRPC adapter, aggregate coordination, WPF, or deployment.
