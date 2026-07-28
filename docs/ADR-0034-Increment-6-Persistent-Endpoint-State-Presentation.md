# ADR-0034 Increment 6 — Persistent Endpoint-State Presentation

## Status

Implemented for validation.

## Scope

This increment introduces a dedicated runtime-inventory presentation model.

`RuntimeInventoryViewModel` owns the published endpoint collection.
`DesktopRuntimeEndpointViewModel` represents one stable endpoint row.

Inventory refreshes now:

- preserve an existing endpoint view model while its authoritative endpoint
  identity remains published;
- update display name, connection state, and attachment generation in place;
- add newly published endpoints;
- remove ended attachments; and
- retain deterministic display ordering.

The WPF timer and immutable in-process snapshot source remain unchanged.
No gRPC self-client path or runtime lifecycle responsibility is introduced.

The endpoint view model also exposes `IsReady` and `IsRecovering` presentation
flags as a foundation for later visual state styling.
