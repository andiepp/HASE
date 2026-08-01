# ADR-0043 — Increment 43B3 — Repeatable Self-Contained Release Publication

## Status

Approved and implemented on 2026-08-01.

## Decision

The Desktop Runtime Host is published by the repository-owned
`tools/Deployment/Publish-HaseDesktopRuntimeHost.ps1` script. The caller must
provide one explicit, fully qualified installation directory.

The script publishes `Hase.DesktopHost.App` for `Release`, `win-x64`, and
self-contained deployment into a temporary staging directory. It verifies the
expected executable before replacing the installed `Application` directory.
An existing application is moved to a uniquely named backup and restored when
installation fails.

The installation boundary separates replaceable application files from
machine-local custody:

```text
<installation>\Application
<installation>\Configuration
<installation>\Identity
```

Only `Application` participates in replacement. `Configuration` and `Identity`
are created when absent and are never removed or overwritten by publication.
Certificate private keys remain in operating-system custody and are not part of
the published payload.

Filesystem roots, relative paths, the repository root, and directories inside
the repository are rejected. The completion summary reports paths and build
classification but never reads or prints configuration content, private
addresses, identities, certificates, or credentials.

Profile creation, launcher creation, desktop shortcuts, and physical validation
remain Increment 43B4.
