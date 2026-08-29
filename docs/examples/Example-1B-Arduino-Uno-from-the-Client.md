# HASE Example 1B — Operating the Arduino Uno from the Client

This example runs the Runtime Host **and** the Client together on one PC
and operates the Arduino Uno's built-in LED from the Client — the same
hardware as [Example 1](Example-1-Arduino-Uno.md), still on the
certificate-free loopback development profile. Where Example 1 works the
endpoint through the Runtime Host's own window, this example adds the
second application and the client-side view of the same normalized model.

## Prerequisites

A completed [Example 1](Example-1-Arduino-Uno.md): the Arduino Uno is
flashed with the Compact Serial Protocol firmware and its endpoint is
composed into your development Runtime Host. Connect the board via USB.

## Verify the development composition

If your development configuration was created or regenerated after
Example 1 — for example by re-running the Example 0 setup — the Arduino
composition may be missing, and the host will publish **only** the
simulation endpoint. That is the designed behavior of the configuration
on disk, not a fault. To check and repair, with both applications closed,
run this block; it writes the endpoint-composition file and rewrites the
development host profile to reference it. Your identity, client
configuration, and registry files stay unchanged:

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$hase = Join-Path $env:LocalAppData "HASE\Development"

@"
{
  "formatVersion": 1,
  "endpoints": [
    {
      "kind": "CompactSerial",
      "expectedEndpointId": "arduino-uno-01",
      "vendorId": 9025,
      "productId": 67,
      "baudRate": 115200,
      "verificationTimeoutMilliseconds": 3000
    }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "desktop-runtime-endpoints.json")

@"
{
  "formatVersion": 1,
  "profileKind": "development-loopback",
  "identityFilePath": "$($hase.Replace('\', '\\'))\\runtime-host-identity.json",
  "endpointCompositionFilePath": "$($hase.Replace('\', '\\'))\\desktop-runtime-endpoints.json",
  "loopbackAddress": "127.0.0.1",
  "port": 52110,
  "includeByteBufferSimulation": true
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "desktop-runtime-development.json")

Get-ChildItem $hase
```

The vendor and product values are decimal: `9025` is `0x2341` and `67` is
`0x0043` — the official Arduino Uno R3. For a compatible board, use your
board's decimal values from Device Manager, as in Example 1.

## Start the Runtime Host

From the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    --development (Join-Path $env:LocalAppData "HASE\Development\desktop-runtime-development.json")
```

Expected result: **two** published endpoints — `arduino-uno-01` and
`simulation-byte-buffer-validation` — both `Ready`. If only the
simulation endpoint appears, run the composition block above; if the
board was connected after the start, press `Refresh`.

## Start the Client

In a second PowerShell window, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.Client.Wpf.App\bin\Release\net10.0-windows\Hase.Client.Wpf.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Development\client-runtime-hosts.json")
```

Press `Connect` on the development host entry. The tile turns green and
lists both endpoints.

## Operate the LED from the Client

Select `arduino-uno-01` and its controller instrument:

1. **Write the Property** — set `Led/State` to `true`. The board's
   built-in LED (`LED_BUILTIN`, marked `L`) lights, and the displayed
   value is the endpoint-confirmed readback — the device reported the
   state, the Client did not assume it.
2. **Execute the Command** — `Led/Toggle`. The physical LED flips and
   `Led/State` follows after the authoritative readback.
3. **Bonus** — with the Example 1 wiring in place, `Analog/Voltage`
   follows the potentiometer live, and each button press produces one
   `Controller/ButtonPressed` occurrence in the Event feed.

Host window and Client window show the same model at the same time — a
change made in either is visible in both.

## Troubleshooting

- **Only the simulation endpoint publishes** — the development profile
  carries no Arduino composition. Run the composition block above.
- **The endpoint stays absent although the composition is present** —
  another Runtime Host deployment may hold the serial port. A computer
  can run several host deployments (for example an installed
  desktop-shortcut host beside this development host); close the others,
  and close any serial monitor, then press `Refresh`. Check with:

  ```powershell
  @(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count
  ```

  Expected while running this example: `1`.

## Where to go next

[Example 2 — ESP32 in the local network](Example-2-ESP32.md) continues
the ladder with a Wi-Fi instrument. From there, Example 3 takes the
Client to a second PC under mutual TLS.
