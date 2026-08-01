# ADR-0043 — Increment 43B1 — Single-Profile Runtime Host Startup

## Status

Approved and implemented on 2026-08-01.

## Decision

The Desktop Runtime Host accepts one fully qualified application-profile path.
That profile references the installation identity, private-network deployment,
and endpoint-composition files and retains the diagnostics and optional
simulation settings.

The existing format-version-1 installation profile remains readable. When its
new `endpointCompositionFilePath` property is absent, the compatibility contract
resolves `desktop-runtime-endpoints.json` beside the private-network file. New
profiles must write the reference explicitly; the convention exists only so the
committed 43A3 reader contract is not silently invalidated.

Startup loads and validates the installation profile, endpoint composition, and
private-network deployment before creating backend state. The current backend
still requires exactly one native-network endpoint; consuming the complete
composition and installation identity is reserved for 43B2.

The older multi-argument command line remains temporarily available for the
existing Visual Studio Debug launch profile. It is not the Release deployment
contract and will be removed after the new startup path is physically validated.
