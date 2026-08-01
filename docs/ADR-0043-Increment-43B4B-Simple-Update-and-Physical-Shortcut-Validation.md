# ADR-0043 — Increment 43B4B — Simple Update and Physical Shortcut Validation

## Status

Approved, implemented, and physically validated on 2026-08-01.

## Decision

The installed Desktop Runtime Host is updated through the parameterless
`Update-HaseDesktopRuntimeHost.ps1` operator script. It targets the guided
user-local installation, validates the expected profiles, identity directory,
published executable, and exact shortcut target, argument, and working
directory before invoking the lower-level publisher.

SHA-256 hashes of the application profile, endpoint composition,
private-network configuration, desktop shortcut, and optional persisted
identity are captured before and after publication. Any custody change causes
the update to fail. Profile contents, endpoint addresses, identity values,
certificate references, and credentials are never displayed.

The operator experience is therefore:

- install once with `Install-HaseDesktopRuntimeHost.ps1`;
- start normally through the `HASE Runtime Host` desktop shortcut; and
- update with `Update-HaseDesktopRuntimeHost.ps1`.

## Validation

The automated baseline completed with 4,183 passing tests after the corrective
asynchronous window-shutdown change.

Physical validation confirmed:

- parameterless application update with configuration, identity, and shortcut
  preservation;
- startup through the desktop shortcut with one application-profile argument;
- Ready native ESP32 and compact Arduino Uno endpoints;
- Operational, Protocol, and Bytes diagnostic capture and presentation;
- authenticated mutual-TLS client snapshot, Property, reversible Arduino state
  change, and physical Event delivery;
- orderly client, diagnostics-window, Runtime Host, endpoint, and process
  shutdown;
- successful restart through the same shortcut; and
- unchanged installation-identity file hash across restart and shutdown.

An overlapping second Runtime Host instance was shown to contend for exclusive
compact serial ownership. Explicit single-instance protection remains the next
corrective deployment increment and is not treated as successful concurrent
Runtime Host operation.
