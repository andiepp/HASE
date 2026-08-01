# ADR-0043 — Increment 43B4A — Guided One-Time Runtime Host Installation

## Status

Approved and implemented on 2026-08-01.

## Decision

One operator-facing script, `Install-HaseDesktopRuntimeHost.ps1`, performs the
one-time user-local Desktop Runtime Host installation. It asks only for the
existing private-network configuration file and the ESP32 host name or address.

The installation uses the reviewed physical defaults: native endpoint identity
`doit-esp32-devkitc-v4-01` on TCP port 5000; compact endpoint identity
`arduino-uno-01`, USB VID 0x2341, PID 0x0043, 115200 baud, and a three-second
verification timeout. Maximum diagnostics is `Bytes`; the ByteArray simulation
is disabled.

The script invokes the lower-level Release publisher, copies the selected
private-network file into configuration custody, writes strict versioned
application and endpoint-composition profiles, and creates a `HASE Runtime Host`
desktop shortcut. The shortcut targets the published executable, supplies the
application-profile path as its only argument, and uses the application
directory as its working directory.

Existing profiles, copied private-network configuration, and shortcuts are never
overwritten. A partial profile/shortcut installation is removed on failure;
published application files remain safely updateable through the separate
publisher. Private-network contents and the ESP32 host are not printed in the
completion summary.

The generated document contracts are covered by automated round-trip tests
through the production strict readers. Physical shortcut startup and combined
endpoint validation remain Increment 43B4B.
