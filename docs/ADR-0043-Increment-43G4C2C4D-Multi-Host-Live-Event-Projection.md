# ADR-0043 Increment 43G4C2C4D — Multi-Host Live Event Projection

## Status

Complete. Automated and physical validation succeeded on 2026-08-02.

## Physical finding

The MiniPC Runtime Host displayed the Arduino pushbutton Event and the laptop
Client diagnostics recorded the corresponding `EventOccurred` observation, but
the Client main-window Live Events list remained empty.

This proves that the southbound Event path, Runtime Host publication, mutual-TLS
session, and northbound observation stream are operational.

## Cause

The normalized Client session raises `EventOccurred` before it publishes the
reduced observation state. The main-window dispatcher therefore first adds the
Live Event and then applies the same selected host's refreshed snapshot.

Every multi-host snapshot application cleared the Live Events collection,
including routine observation-state refreshes within an unchanged connected
session. The newly added Event was consequently removed immediately.

## Decision

Routine snapshot refreshes for the same selected connected Runtime Host preserve
the Live Events collection.

The collection remains scoped to the selected connection and is cleared when:

- the selected Runtime Host changes;
- no selected session remains;
- the selected session becomes disconnected, connecting, disconnecting, or
  faulted; or
- a reconnecting session establishes a new connected boundary.

No Runtime Host, protocol, authentication, diagnostics, Property, or Command
behavior changes in this increment.

## Automated validation

Focused regression coverage verifies the physical Event ordering:

1. an Event from the selected MiniPC profile is projected;
2. the immediately following same-host observation-state refresh preserves it;
3. a new connection boundary still clears the transient Event collection.

Existing coverage continues to require Events from an unselected Runtime Host
to be ignored.

The complete Visual Studio 2026 Release suite passed with 4,405 tests.

## Physical validation

After a successful Release build and complete automated test run:

1. install the updated laptop Client while preserving its configuration;
2. start the MiniPC Runtime Host;
3. start the laptop Client and connect the MiniPC profile;
4. press and release the Arduino pushbutton once;
5. confirm one `EventOccurred` record appears in Client diagnostics;
6. confirm one corresponding item remains visible in the main-window Live Events
   list;
7. press and release the button again and confirm exactly one additional Live
   Event appears; and
8. stop the Client and Runtime Host cleanly.

Both button presses produced one diagnostic record and one persistent Live
Event each. Subsequent simultaneous-host validation confirmed that Desktop and
MiniPC Events remained attributed to their originating Runtime Hosts.
