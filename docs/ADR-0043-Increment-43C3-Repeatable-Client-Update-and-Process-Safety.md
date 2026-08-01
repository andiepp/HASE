# ADR-0043 — Increment 43C3 — Repeatable Client Update and Process Safety

## Status

Approved, implemented, and physically validated on 2026-08-01.

## Decision

`Update-HaseClient.ps1` is the parameterless operator-facing update command for
the guided user-local client installation. It verifies the installed
configuration file and desktop shortcut, refuses to update while the client is
running, invokes the transactional Release publisher, and verifies that
configuration and shortcut hashes remain unchanged.

The updater never reads or prints configuration contents, private-network
addresses, certificate references, certificate pins, identities, or
credentials. Certificate private keys remain in Windows certificate-store
custody.

The WPF client owns a user-session named-mutex lease for its complete process
lifetime. A duplicate launch presents a bounded informational message and exits
without constructing a second client shell or session. Normal application exit
releases the lease after diagnostics and client-session cleanup.

The final operator workflow is:

1. Run `Install-HaseClient.ps1` once.
2. Start `HASE Client` from the desktop shortcut.
3. Close the client and run `Update-HaseClient.ps1` for subsequent application
   updates.

Multi-host profile registry activation remains Increment 43D. Installer signing
or packaging beyond repository-owned PowerShell scripts remains deployment
hardening backlog.

## Validation

- The complete Visual Studio suite passed with 4,212 tests.
- The parameterless updater installed the new self-contained Release application
  while preserving the installed client configuration and desktop shortcut.
- An update attempt while the client was running was rejected before
  publication, and the active client remained connected.
- A duplicate shortcut launch displayed the bounded single-instance message and
  did not create a second client process or session.
- The original client retained its private-network connection, presented both
  published endpoints, completed a Property read, and continued receiving live
  updates.
- Normal window closure terminated the client process and released the
  single-instance lease; a subsequent shortcut launch connected normally.
- No private-network addresses, certificate references, certificate pins,
  identities, credentials, or configuration contents were recorded here.
