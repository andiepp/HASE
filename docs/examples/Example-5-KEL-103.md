# HASE Example 5 — A laboratory instrument (KEL-103)

This example brings a real laboratory instrument into HASE: the KORAD
KEL-103 programmable DC electronic load, attached over USB serial through
HASE's SCPI adapter boundary. The instrument's identity, firmware,
measurements, operating mode, input state, and targets appear as ordinary
normalized Properties — the same model every other example used. It runs
on the certificate-free development profile from
[Example 0](../Getting-Started.md).

Two things distinguish SCPI endpoints from the earlier examples:

- **They are configured, never discovered.** You state the COM port
  explicitly; nothing scans for instruments.
- **There is no SCPI console.** HASE deliberately exposes only the
  normalized Properties and Commands of a versioned instrument
  definition — device-specific syntax stays below the boundary.

The primary path of this example is **read-only**. Controlled operation —
setpoints, modes, input switching — is the advanced section at the end,
with its safety model explained first.

## Parts and prerequisites

- A KORAD KEL-103 connected over USB. Windows shows its serial adapter as
  a COM port (Device Manager → Ports); note the number.
- A completed Example 0 (built clone, development configuration).
- For the advanced section only: the standing laboratory rule from this
  project's own validations — keep any external supply connected to the
  load **switched off** while you experiment with modes and setpoints.

## Add the endpoint to the development Runtime Host

Both applications closed, run this block with your COM port. It writes a
KEL-103-only composition; to keep instruments from Examples 1 and 2, add
their entries to the same `endpoints` array as before:

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$hase = Join-Path $env:LocalAppData "HASE\Development"

@"
{
  "formatVersion": 1,
  "endpoints": [
    {
      "kind": "Kel103Serial",
      "expectedEndpointId": "kel-103-01",
      "definitionId": "kel103-identity",
      "definitionVersion": 3,
      "serialPort": "COM5",
      "baudRate": 115200
    }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "desktop-runtime-endpoints.json")
```

The fields, exactly:

- `expectedEndpointId` — the logical identity you choose for this
  instrument; the host publishes it under this name.
- `definitionId` / `definitionVersion` — the versioned KEL-103 instrument
  definition. Version `3` is the complete **read-only** view (identity,
  firmware, measured voltage, current, and power, operating mode, input
  state, and the four targets). Versions `4` and `5` add controlled
  operation — see the advanced section. Version `2` is a smaller
  measurements-only slice.
- `serialPort` / `baudRate` — the COM port from Device Manager and the
  characterized `115200`.

## Start and verify

This example uses the **development** pair — the loopback host started
with `--development` and the local Client — not the secured hosts of
Examples 3 and 4. If a secured Runtime Host is running on this machine,
close it first (one Runtime Host per machine, and the instrument's COM
port has one owner). A remote Client cannot see the development host: it
is loopback-only by design.

Start the development Runtime Host from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    --development (Join-Path $env:LocalAppData "HASE\Development\desktop-runtime-development.json")
```

Then start the local Client in a second window, also from the repository
root, and press `Connect` on `Development (loopback, no TLS)`:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.Client.Wpf.App\bin\Release\net10.0-windows\Hase.Client.Wpf.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Development\client-runtime-hosts.json")
```

Expected result: `kel-103-01` publishes and becomes `Ready` after the
host verifies the instrument's identity over SCPI (`KEL-103` product
identity) and synchronizes all Properties. An unreachable port or absent
instrument is tolerated at startup with a warning — fix the connection
and press `Refresh`.

Select the endpoint in the Client. The KEL-103 Electronic Load instrument
shows read-only Properties for product identity, firmware version, the
live measured voltage, current, and power, the operating mode (`CC`,
`CV`, `CR`, `CW`, or `SHORt`), the input state (`ON`/`OFF`), and the four
mode targets.

Things to try, all read-only:

1. Watch the measured values track the instrument.
2. Change the mode or a setpoint **on the instrument's front panel** and
   watch the Properties follow.
3. Unplug the USB cable: the endpoint leaves `Ready` and passive health
   supervision reports the loss. Replug: the host re-verifies the
   identity, resynchronizes every Property, and returns to `Ready` — any
   front-panel changes made while offline are simply adopted, never
   replayed.

## Advanced — controlled operation

Read this section before switching to it. The controlled definition obeys
the safety model physically validated in this project:

- Mode changes and setpoint writes require the input to be
  **authoritatively OFF** — the host refuses them while the load draws.
- Every mutation is transmitted **once**, read back authoritatively, and
  **never retried and never replayed** during recovery. An uncertain
  outcome stays visibly uncertain instead of being repeated.
- Generic input activation **rejects SHORT mode**. Activating a short
  circuit is a separate Command that requires an explicit Boolean
  confirmation, and only from authoritative SHORT/OFF state.
- Each setpoint write also selects its mode (a voltage target selects
  CV, current CC, resistance CR, power CW).

To enable it, change one value in the composition and restart the host:

```powershell
$composition = Join-Path $env:LocalAppData "HASE\Development\desktop-runtime-endpoints.json"
(Get-Content $composition -Raw).Replace('"definitionVersion": 3', '"definitionVersion": 5') | Set-Content $composition -Encoding utf8
```

Version 5 makes the four targets writable and adds the five mode-selection
Commands, `Input.Activate` and `Input.Deactivate`, and the separately
confirmed `ShortCircuit.Activate`. With the external supply output off:

1. With the input OFF, write a current target — the mode follows to CC
   and the readback confirms.
2. Select modes through the Commands; `SHORT` selection alone does not
   activate anything.
3. Activate the input, watch the measurements, deactivate it.
4. Note what the host refuses: setpoint writes while the input is ON, and
   generic activation in SHORT mode — the interlocks working as designed.

## Troubleshooting

- **The endpoint never becomes `Ready`** — wrong COM port, the port is
  held by another program, or the device did not identify as a KEL-103;
  the startup warning names the category. SCPI endpoints are configured:
  a wrong port is not searched around.
- **A setpoint write is refused** — the input is ON; controlled changes
  require authoritative OFF first.
- **A Command outcome is reported uncertain** — the connection failed
  mid-operation; HASE deliberately does not retry mutations. Check the
  instrument's actual state after recovery.
- The composition rejects `definitionVersion` values other than 2, 3, 4,
  or 5.

## Where to go next

To bring your **own** SCPI instrument into HASE, continue with the
[SCPI Instrument Authoring Guide](../SCPI-Instrument-Authoring-Guide.md),
which walks the characterization-first authoring discipline using the
KEL-103 implementation as the worked reference.
