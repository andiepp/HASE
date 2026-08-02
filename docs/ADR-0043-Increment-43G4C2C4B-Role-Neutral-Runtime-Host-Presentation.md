# ADR-0043 — Increment 43G4C2C4B — Role-Neutral Runtime Host Presentation

## Discussion

The serial-only MiniPC Runtime Host passed its complete southbound physical
validation, but its window retained the original product title "HASE Desktop
Runtime Host". That label now incorrectly implies that every installation is
the Desktop machine.

The executable is one reusable Runtime Host product. Installation-specific
names such as "Desktop Runtime Host" and "MiniPC Runtime Host" belong to the
Client-local profile registry. The Runtime Host shell therefore uses the
role-neutral product title "HASE Runtime Host".

## Implement now

- Change `MainWindowViewModel.ApplicationTitle` to `HASE Runtime Host`.
- Use the same title and wording in the single-instance operator dialog.
- Update the existing application-title assertion.
- Preserve all internal identifiers and deployment custody.

Expected full-suite result: **4,396 passed, 0 failed**.

After automated validation, update only the MiniPC Runtime Host application.
Its Configuration directory, installation identity, certificates, shortcut,
and laptop Client profile remain unchanged.

## Backlog

- Validate the laptop Client connection to the MiniPC profile.
- Validate simultaneous Desktop and MiniPC Runtime Host operation.

## Stop point

Stop after the updated MiniPC window displays `HASE Runtime Host` and
`arduino-uno-01` returns to Ready. Do not start the laptop Client yet.
