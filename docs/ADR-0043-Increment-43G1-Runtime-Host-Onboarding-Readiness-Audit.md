# ADR-0043 — Increment 43G1 — Runtime Host Onboarding Readiness Audit

## Decision

An existing guided Windows Runtime Host installation can be audited before it
is used as an onboarding source. The audit is read-only and validates:

- the installed application executable;
- the strict application profile and its guided-installation path custody;
- the strict authoritative installation identity;
- the strict private-network host configuration;
- the referenced client-enrollment file;
- the strict endpoint-composition profile; and
- the desktop shortcut target, working directory, and single profile argument.

The .NET audit reuses the existing strict readers. The PowerShell entry point
adds the Windows desktop-shortcut check. A missing, malformed, or inconsistent
artifact fails the audit; it does not repair, replace, create, enroll, start,
discover, or attach anything.

## Run

Close the HASE Desktop Runtime Host, build the solution in Release, and run
from the repository root:

```powershell
& .\tools\Deployment\Test-HaseDesktopRuntimeHostOnboardingReadiness.ps1
```

If local execution policy blocks the repository script, use the already
validated process-scoped policy:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

## Safe output

Successful output contains the authoritative Runtime Host ID and readiness
states only. It does not print private addresses, certificate thumbprints,
credentials, private configuration contents, enrollment contents, endpoint
addresses, or private keys.

The identities remain distinct:

- Runtime Host ID identifies the installation authoritatively;
- a client-local profile ID identifies one Client registry entry;
- certificate identity authenticates a transport principal; and
- endpoint identity identifies a physical or logical endpoint owned by a
  Runtime Host.

## Deferred

43G1 does not install a second host, provision certificates, modify client
enrollment, create a Client profile, start a Runtime Host, discover endpoints,
or attach endpoints. Those mutating onboarding steps require later explicit
increments and physical approval.
