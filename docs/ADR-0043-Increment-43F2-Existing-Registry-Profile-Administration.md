# ADR-0043 — Increment 43F2 — Existing-Registry Profile Administration

## Decision

`Hase.Client.RegistryTool` performs explicit offline administration of an existing client Runtime Host registry. Supported operations are:

- `add` — append one explicitly identified profile in registry order;
- `enable` — enable one exact profile identity;
- `disable` — disable one exact profile identity; and
- `remove` — remove one exact profile identity after repeating that identity as confirmation.

The HASE Client must be closed. Every operation loads the active document through `PrivateNetworkRuntimeHostProfileRegistryFile`, applies the immutable registry contracts, writes a same-directory candidate, reloads that candidate through the same strict reader, and atomically replaces the active file. The prior active registry is retained at the backup path reported by the tool.

## Commands

Run from the repository root. Quote every path or display name containing spaces.

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -- `
  add "<registry-path>" <profile-id> "<display-name>" `
  <expected-runtime-host-id> "<private-network-configuration-path>" true
```

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -- `
  disable "<registry-path>" <profile-id>
```

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -- `
  enable "<registry-path>" <profile-id>
```

Removal requires the exact profile identity twice:

```powershell
dotnet run --project .\src\Hase.Client.RegistryTool -- `
  remove "<registry-path>" <profile-id> <profile-id>
```

## Safety boundaries

- Duplicate profile identities and conflicting enabled expected-host identities fail before replacement.
- `add` requires an existing fully qualified private-network configuration path.
- Enable, disable, and remove require an existing exact profile identity.
- Removal never deletes or modifies the referenced private-network configuration.
- An existing requested backup is never overwritten.
- Console output contains the operation, profile identity, and registry-backup path only; it does not display configuration contents, credentials, certificates, thumbprints, addresses, or hostnames.
- The registry tool performs no discovery, certificate enrollment, host startup, endpoint onboarding, or network access.

## Recovery

Keep the reported backup until the edited registry has started and connected successfully. To recover, close the HASE Client, preserve the rejected active registry for investigation, and restore the reported backup to the active registry path.
