# ADR-0043 — Increment 43G3 — Disabled Client Profile from Handoff

## Decision

`Hase.Client.RegistryTool` accepts an existing strict 43G2 Runtime Host
handoff as the authoritative expected-host identity for one new Client-local
profile. Import always creates the profile disabled. Enabling and connecting
remain separate explicit operations after local private-network configuration,
certificate custody, enrollment, and identity have been verified.

## Command

Close the HASE Client and run from the repository root:

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -c Release --no-build -- `
  add-from-handoff "<registry-path>" <new-profile-id> "<display-name>" `
  "<handoff-path>" "<existing-local-private-network-client-configuration>"
```

The tool loads the handoff through its strict reader and obtains only the
authoritative Runtime Host ID. The profile ID, display name, registry, and
private-network Client configuration are explicit local inputs.

## Safety

- The imported profile is always disabled; the command has no enable option.
- The existing registry editor validates the strict candidate, atomically
  replaces the active registry, and retains its prior state as a timestamped
  backup.
- Duplicate Client-local profile IDs fail closed.
- The referenced private-network Client configuration must already exist.
- The handoff and referenced configuration are neither modified nor deleted.
- No certificate, enrollment, shortcut, connection, discovery, Runtime Host,
  or endpoint lifecycle state changes.

Successful output contains only the operation, Client-local profile ID,
expected Runtime Host ID, and backup path. Private addresses, certificate
thumbprints, credentials, and configuration contents remain undisclosed.

## Later enablement

Before enablement, compare the imported expected Runtime Host ID with the
source installation, confirm certificate and enrollment custody through the
approved private-network provisioning workflow, and validate the local Client
configuration. Enablement and first connection are separate physical checks.
