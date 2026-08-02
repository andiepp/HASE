# ADR-0043 — Increment 43H — Combined Multi-Host Validation and Closure

## Status

Complete. ADR-0043 closed on 2026-08-02.

## Automated validation

The complete Visual Studio 2026 Release suite passed with 4,405 tests after the
final multi-host Live Event projection correction.

## Installed topology

- The Desktop Runtime Host owned its configured physical Arduino and ESP32
  endpoints.
- The independently installed MiniPC Runtime Host owned its configured physical
  Arduino endpoint.
- The laptop Client contained two enabled profiles with distinct authoritative
  Runtime Host identities and maintained both mutual-TLS sessions concurrently.
- Runtime Host and Client application updates preserved external configuration,
  identity, certificate-store custody, and desktop shortcuts.

No private-network address, certificate identity, credential, or other
deployment-sensitive value is recorded in source documentation.

## Physical validation

The combined topology validated:

- simultaneous startup and operation of both Runtime Hosts;
- independent Client connection to both enabled profiles;
- host-scoped endpoint inventory, including identical endpoint identifiers
  remaining distinct across Runtime Hosts;
- authoritative Property reads and updates against the selected host;
- Command execution and authoritative Property reconciliation against the
  selected host;
- physical Events in Client diagnostics and the main-window Live Events list;
- correct Event attribution without cross-host leakage;
- serial endpoint unplug/reconnect and hardware-reset recovery;
- independent disconnection and reconnection of one host without disruption of
  the other session; and
- orderly Client and Runtime Host shutdown.

## Closure

ADR-0043 delivered repeatable Release publication, guided installation and
update, installation-safe Runtime Host identity, external host composition and
Client registry profiles, explicit mutual-TLS enrollment and trust, independent
multi-host sessions, host-scoped presentation and diagnostics, safe profile and
endpoint administration, non-secret onboarding handoffs, and physically
validated simultaneous multi-host operation.

The architecture retains the established security and ownership boundaries:

- Tailscale supplies reachability only;
- certificate authentication, exact server-certificate pinning, explicit
  enrollment, and expected Runtime Host identity verification remain mandatory;
- each Runtime Host exclusively owns its physical endpoint lifecycles; and
- the Client cannot remotely attach, detach, or replace endpoints.

ADR-0043 is accepted and complete.

## Deferred

- automatic Tailscale discovery;
- remote Runtime Host lifecycle administration;
- remote endpoint attachment, detachment, or replacement;
- centralized fleet orchestration and failover;
- automatic certificate-authority operation, renewal, or rotation;
- Python automation;
- SCPI integration;
- remote media streaming; and
- diagnostic export and offline analysis.
