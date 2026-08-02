# ADR-0043 — Increment 43G4A — Native-Network Endpoint Profile Administration

## Decision

`Hase.DesktopHost.EndpointProfileTool` performs explicit offline add and
confirmed remove operations against an existing endpoint-composition profile.
The Desktop Runtime Host must be closed. Every candidate passes the immutable
composition contracts and existing strict reader before atomic replacement;
the prior composition is retained as a timestamped backup.

```powershell
dotnet run --project .\src\Hase.DesktopHost.EndpointProfileTool -c Release --no-build -- `
  add-native "<composition-path>" <expected-endpoint-id> "<host-or-address>" <port>
```

```powershell
dotnet run --project .\src\Hase.DesktopHost.EndpointProfileTool -c Release --no-build -- `
  remove-native "<composition-path>" <expected-endpoint-id> <expected-endpoint-id>
```

The host or address is external operational configuration and is never printed
by the tool. Duplicate identities across native and compact endpoint kinds,
invalid host values, invalid ports, unknown removals, and existing backup paths
fail before active replacement.

The tool performs no network access, discovery, verification, attachment,
detachment, replacement, or Runtime Host startup. First startup and physical
identity validation remain a separate approved step. Removal changes only the
composition profile and never affects endpoint firmware or credentials.
