# ADR-0043 — Increment 43C1 — Client Release Publication

## Status

Approved and implemented on 2026-08-01; publication validation pending.

## Decision

The WPF client is published by the repository-owned
`tools/Deployment/Publish-HaseClient.ps1` script. The caller supplies one
explicit fully qualified installation directory.

The script publishes `Hase.Client.Wpf.App` for `Release`, `win-x64`, and
self-contained deployment into a temporary staging directory. It verifies the
expected executable before replacing the installed `Application` directory.
An existing application is moved to a uniquely named backup and restored if
installation fails.

The installation separates replaceable application files from machine-local
configuration custody:

```text
<installation>\Application
<installation>\Configuration
```

Only `Application` participates in replacement. `Configuration` is created when
absent and is never removed or overwritten by publication. Client certificate
private keys remain in Windows certificate-store custody and are not part of the
published payload.

Filesystem roots, relative paths, the repository root, and directories inside
the repository are rejected. The summary never reads or prints client profile
contents, addresses, certificate references, pins, or credentials.

Guided profile installation, shortcut creation, single-instance behavior, and
physical mutual-TLS validation remain 43C2 and 43C3.
