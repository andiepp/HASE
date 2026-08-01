# ADR-0043 — Increment 43C2 — Guided Client Installation and Desktop Shortcut

## Status

Approved, implemented, and physically validated on 2026-08-01.

## Decision

One operator-facing script, `Install-HaseClient.ps1`, performs the one-time
user-local WPF client installation. It asks only for the existing laptop
private-network client JSON file.

The script invokes the lower-level Release publisher and requires a fully
qualified, existing source configuration file before copying it into
configuration custody. The installed client remains the authority for semantic
configuration validation when it starts from the shortcut. The Windows
PowerShell installer does not load .NET 10 application assemblies into its
Windows PowerShell 5.1 process.

The installed configuration path is
`Configuration\laptop-private-network.json`. The `HASE Client` desktop shortcut
targets the published WPF executable, supplies that configuration path as its
only argument, and uses the application directory as its working directory.
The existing WPF startup behavior therefore opens and connects automatically.

Existing installed configuration and shortcuts are never overwritten. Partial
configuration/shortcut installation is removed on failure. Configuration
contents, server addresses, certificate references, pins, identities, and
credentials are never printed. Certificate private keys remain in Windows
certificate-store custody.

Multi-host registry activation remains 43D. Parameterless updates,
single-instance behavior, and mutual-TLS physical validation remain 43C3.

## Validation

- The complete Visual Studio suite passed with 4,205 tests.
- A self-contained Release installation completed on the Windows 11 laptop.
- The installer placed the selected client configuration into installation
  custody and created the `HASE Client` desktop shortcut.
- Shortcut startup supplied the installed configuration without opening a file
  selection dialog.
- The client connected successfully to the private-network Runtime Host and
  presented its published endpoints.
- No private-network addresses, certificate references, certificate pins,
  identities, credentials, or configuration contents were recorded here.
