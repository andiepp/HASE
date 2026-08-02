# ADR-0043 — Increment 43F3 — Client Enrollment Administration Runbook

## Purpose

This runbook closes the 43F client-enrollment work. It records the operational
boundary validated with the installed HASE Client and the repository-owned
`Hase.Client.RegistryTool`. It never substitutes profile administration for
certificate provisioning or private-network configuration.

## Preconditions

Before any registry operation:

1. close every HASE Client process;
2. identify the fully qualified active `client-runtime-hosts.json` path;
3. identify the existing private-network client configuration to reference;
4. obtain the exact client-local profile ID, display name, and expected
   authoritative Runtime Host ID through an approved deployment channel; and
5. retain existing timestamped backups until the resulting installation has
   been validated.

Run the tool from the repository root. Repository execution is an
administrative development workflow; it does not imply that the tool is
installed with the self-contained HASE Client application.

## Add a profile

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -c Release --no-build -- `
  add "<registry-path>" <profile-id> "<display-name>" `
  <expected-runtime-host-id> "<private-network-configuration-path>" false
```

Adding a profile disabled is the safe default. Inspect the registry and its
reported backup before explicitly enabling the profile. Addition fails before
replacement when the profile ID is duplicated, the referenced configuration
does not exist, or the candidate violates the strict registry contracts.

## Enable or disable a profile

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -c Release --no-build -- `
  enable "<registry-path>" <profile-id>
```

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -c Release --no-build -- `
  disable "<registry-path>" <profile-id>
```

The exact profile must already exist. Enabling fails closed when another
enabled profile already expects the same authoritative Runtime Host identity.
A rejected candidate leaves both the active registry and backup inventory
unchanged.

## Remove a profile

Removal requires the exact profile ID twice:

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -c Release --no-build -- `
  remove "<registry-path>" <profile-id> <profile-id>
```

Removal changes only the registry. It never deletes or modifies the referenced
private-network configuration, certificate material, or enrollment data.

## Validate a successful operation

After each successful operation:

1. record the backup path reported by the tool;
2. confirm that the backup exists and contains the prior registry state;
3. inspect only the non-secret registry projection: profile ID, display name,
   expected host identity, referenced configuration path, and enabled state;
4. start the HASE Client from its installed shortcut;
5. confirm the intended profiles and enabled states; and
6. confirm that each intended enabled profile connects only to its expected
   authoritative Runtime Host.

Do not expose private addresses, certificate thumbprints, credentials, private
configuration contents, or private keys in source, documentation, screenshots,
or ordinary command output.

## Recover the prior registry

If validation fails:

1. close the HASE Client;
2. preserve the rejected active registry under a distinct diagnostic name;
3. copy the reported operation backup over the active registry path;
4. inspect the restored registry;
5. start the Client and validate the restored profiles and connections; and
6. retain both the rejected registry and backup until the cause is understood.

Restoration changes the registry only. Referenced private-network
configuration remains under its existing deployment custody.

## Backup retention and cleanup

Timestamped operation backups and temporary validation copies are recovery
evidence. Keep them until the edited registry has passed startup, profile,
identity, connection, and endpoint-inventory checks. Cleanup is a separate,
explicit administrative action performed with the Client closed. Never use a
broad wildcard deletion against the HASE configuration directory.

## Validated 43F result

- Existing single-host installation migrated to registry startup successfully.
- Installed application update preserved registry and shortcut custody.
- Add created a disabled profile and a recoverable prior-state backup.
- Conflicting enablement failed without changing registry or backup inventory.
- Confirmed remove preserved the referenced private-network configuration.
- The removal backup restored successfully and was accepted by the Client.
- The intended one-profile registry was restored and connected successfully.
- Both physical endpoints remained visible through the Desktop Runtime Host.
- The complete automated suite passed with 4,305 tests.

43F does not perform discovery, certificate enrollment, Runtime Host lifecycle
administration, endpoint onboarding, or combined physical two-host validation.
Those boundaries remain assigned to later ADR-0043 increments.
