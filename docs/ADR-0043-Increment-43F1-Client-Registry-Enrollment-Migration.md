# ADR-0043 — Increment 43F1 — Client Registry Enrollment Migration

## Decision

The guided HASE Client installation now owns two distinct external artifacts:

- `laptop-private-network.json` retains the existing private-network client configuration and certificate references;
- `client-runtime-hosts.json` identifies one or more client-local Runtime Host profiles and references their private-network configurations.

The client desktop shortcut receives exactly one argument: the fully qualified Runtime Host registry path. New guided installations ask for the client-local profile identity, display name, and expected authoritative Runtime Host identity before creating the initial registry.

Existing guided single-host installations migrate with `Migrate-HaseClientToRuntimeHostRegistry.ps1`. Migration preserves the existing private-network configuration, creates the registry without overwriting an existing registry, and changes only the desktop shortcut argument. A temporary shortcut backup is restored if migration cannot be verified.

Tracked `launchSettings.json` no longer contains a user-specific path. A developer supplies a local registry path through untracked Visual Studio launch configuration or an explicit command line.

## Safety boundaries

- No certificate, thumbprint, network address, credential, or private-network configuration content is copied into source control or displayed by the migration.
- Profile and expected Runtime Host identities remain distinct.
- The existing strict registry reader remains authoritative.
- Existing registries are never overwritten by installation or migration.
- Installed application updates preserve and verify registry and shortcut custody.
- Identity mismatch continues to fail closed.

## Deferred to 43F2

- adding another profile to an existing registry;
- disabling, enabling, or removing an enrolled profile;
- atomic registry replacement with recovery evidence; and
- combined two-host enrollment validation.
