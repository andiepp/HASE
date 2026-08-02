# ADR-0043 — Increment 43G4C2A — Second-PC Runtime Host Preflight

## Decision

Before provisioning or installing a second Runtime Host, a read-only Windows
preflight reports ten readiness states: Windows, .NET 10 SDK, Release source,
clean installation and shortcut state, strict private-network configuration,
strict client enrollment, server certificate with private key, local listener-
address ownership, and a matching Arduino USB metadata candidate.

The preflight does not open the candidate COM port, perform Compact Protocol
verification, bind the listener, create an identity, install files, alter the
certificate store, change enrollment, create a shortcut, or modify firewall
state.

## Command

After building Release on the second PC, run from the repository root with its
explicitly provisioned prospective host configuration:

```powershell
& .\tools\Deployment\Test-HaseSecondPcRuntimeHostPreflight.ps1 `
  -PrivateNetworkConfigurationPath "<fully-qualified-second-host-config>" `
  -CompactVendorId "0x2341" `
  -CompactProductId "0x0043"
```

The configuration must belong to the second PC. Never copy the first desktop's
Runtime Host identity directory or server private key as an installation
shortcut.

## Safe output

Output contains `Ready` or `Blocked` for each named check, overall readiness,
and read-only mode. It does not print private addresses, certificate
thumbprints or subjects, credentials, enrollment contents, configuration
contents, COM-port names, or USB serial numbers.

## Consequence

A blocked preflight is evidence for the next explicit provisioning step, not
permission to repair the machine automatically. 43G4C2A makes no changes and
does not authorize installation.
