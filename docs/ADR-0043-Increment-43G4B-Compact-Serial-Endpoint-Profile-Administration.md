# ADR-0043 — Increment 43G4B — Compact-Serial Endpoint Profile Administration

## Decision

The existing offline endpoint-profile editor and tool support explicit compact-
serial add and confirmed remove operations. The Desktop Runtime Host must be
closed. Candidate profiles pass the immutable composition contracts and strict
reader before atomic replacement, with the prior composition retained as a
timestamped backup.

## Commands

```powershell
dotnet run --project .\src\Hase.DesktopHost.EndpointProfileTool -c Release --no-build -- `
  add-compact "<composition-path>" <expected-endpoint-id> `
  <0xVID> <0xPID> <baud-rate> <verification-timeout-ms>
```

USB identifiers require exact `0xNNNN` form. Baud rate must be positive and the
verification timeout must be between 1 and 60,000 milliseconds.

```powershell
dotnet run --project .\src\Hase.DesktopHost.EndpointProfileTool -c Release --no-build -- `
  remove-compact "<composition-path>" <expected-endpoint-id> <expected-endpoint-id>
```

## Safety boundaries

- Endpoint identities are unique across native and compact kinds.
- Removal requires an existing compact endpoint; a native endpoint with the
  same requested identity does not satisfy the operation.
- Existing endpoint order is preserved within each composition kind.
- Existing backup paths are never overwritten.
- Output contains only operation, expected endpoint ID, endpoint kind, and
  backup path.
- The tool does not enumerate USB devices or serial ports and performs no
  verification, attachment, detachment, replacement, or Runtime Host startup.

Physical validation uses a temporary non-matching USB identity while the
Runtime Host is closed, followed by confirmed removal before normal startup.
