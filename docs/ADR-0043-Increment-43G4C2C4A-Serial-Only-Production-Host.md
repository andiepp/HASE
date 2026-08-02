# ADR-0043 — Increment 43G4C2C4A — Serial-Only Production Host

## Discussion

Physical validation of the MiniPC profile exposed an obsolete startup
assumption: installed production profiles were required to contain exactly one
native network endpoint. The MiniPC is intentionally an Arduino-only Runtime
Host, so its valid endpoint composition contains one compact serial endpoint
and no native endpoint.

The production backend already attaches configured endpoints by collection and
calculates the expected publication count from the composition. The defect was
therefore confined to startup parsing and the legacy ESP32 compatibility value.

## Implement now

- Installed profiles accept zero or one native network endpoint.
- More than one native network endpoint remains unsupported and fails closed.
- Endpoint composition continues to require between one and 64 physical
  endpoints, so an empty installed Runtime Host remains invalid.
- The legacy Visual Studio startup form still requires an ESP32 host and
  continues to compose the historical ESP32 plus Arduino topology.
- Six tests cover serial-only parsing and planning, native plus serial
  preservation, multiple-native rejection, empty-composition rejection, and
  legacy null-host rejection.

Expected full-suite result: **4,396 passed, 0 failed**.

After automated validation, update only the installed Runtime Host application
on the MiniPC while preserving its Configuration directory, identity, and
desktop shortcut. Then resume 43G4C2C4 with the Desktop Runtime Host and laptop
Client stopped.

## Backlog

- Present pre-shell startup failures in a visible operator dialog.
- Validate simultaneous Desktop and MiniPC Runtime Host operation.

## Stop point

Stop after the updated MiniPC Runtime Host starts, publishes
`arduino-uno-01`, and retains listener ownership. Do not start the laptop Client
until that isolated host checkpoint succeeds.
