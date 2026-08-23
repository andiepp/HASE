# HASE Example 1 — Arduino Uno on one PC

This example extends a completed [Example 0](../Getting-Started.md) with a
physical instrument: an Arduino Uno connected over USB, running the
repository's Compact Serial Protocol firmware. You will flash the firmware,
add the endpoint to your development Runtime Host, and operate a real LED,
read a real analog voltage, and receive a real push-button Event — still on
one PC, still on the certificate-free loopback development profile.

## Parts list

- One Arduino Uno with its USB cable.
- Optional, for the Event step: one push button and two jumper wires (a
  breadboard helps).
- Optional, for the analog step: one 10 kOhm potentiometer and three jumper
  wires.

Note on compatible boards: the endpoint configuration below matches the USB
identity of the official Arduino Uno (vendor `0x2341`, product `0x0043`).
Many Uno-compatible boards use a different USB serial chip (for example
CH340) with a different identity. They run the same firmware, but you must
look up the board's vendor and product IDs in Windows Device Manager
(*Ports → your board → Details → Hardware IDs*, shown as `VID_xxxx` and
`PID_xxxx` in hexadecimal) and put those values — converted to decimal —
into the configuration below.

## Prerequisites

- A completed Example 0: the repository built in `Release`, and the
  development configuration files in `%LocalAppData%\HASE\Development`.
- The [Arduino IDE](https://www.arduino.cc/en/software) (version 2.x).

The firmware uses no external libraries.

## Flash the firmware

1. Connect the Arduino Uno over USB.
2. In the Arduino IDE, open `HaseArduinoUno\HaseArduinoUno.ino` from your
   clone.
3. Select *Tools → Board → Arduino Uno* and *Tools → Port → the board's
   COM port*.
4. Press *Upload* and wait for `Done uploading.`
5. **Close the Arduino IDE** (or at least any Serial Monitor). The USB
   serial line is the binary HASE transport; a serial monitor holding the
   port prevents the Runtime Host from attaching the endpoint.

The firmware identifies itself as endpoint `arduino-uno-01` with descriptor
`arduino-uno-validation` version 2 at 115200 baud.

## Wire the push button (optional)

The Event step uses a push button between pin `D7` and `GND`. The input is
active-low with the internal pull-up and a 50 ms debounce — no resistor is
needed. Skip this if you only want Properties and Commands; everything else
works without it.

## Wire the analog voltage source (optional)

The analog step reads pin `A0`, which accepts 0 to 5 V. A 10 kOhm
potentiometer makes an adjustable source: connect its two outer pins to
`5V` and `GND`, and its middle pin (the wiper) to `A0`. Turning the knob
then sweeps `A0` smoothly between 0 and 5 V. Without a connected source the
pin floats and reads an arbitrary drifting value.

## Add the endpoint to the development Runtime Host

Both applications closed, run this block. It writes the endpoint-composition
file and rewrites the development host profile to reference it. Your
identity, client configuration, and registry files from Example 0 stay
unchanged.

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
`0x0043`. For a compatible board, replace them with your board's decimal
values from Device Manager.

## Start and verify

Start the Runtime Host exactly as in Example 0, with the Arduino connected:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    --development (Join-Path $env:LocalAppData "HASE\Development\desktop-runtime-development.json")
```

Expected result: the window shows two published endpoints —
`arduino-uno-01` and `simulation-byte-buffer-validation` — both `Ready`.
Attachment takes a few seconds: the host enumerates USB serial candidates,
verifies the authoritative endpoint identity, and synchronizes the
Properties before publication.

If the Arduino was not connected when the host started, the host stays
running with the simulation endpoint only. Connect the board and press
`Refresh`: the endpoint is searched, verified, and attached without a
restart.

Start the Client exactly as in Example 0 and press `Connect`. Selecting the
`arduino-uno-01` endpoint shows its instrument with the Properties
`Led/State` and `Analog/Voltage`, the Command `Led/Toggle`, and the Event
`Controller/ButtonPressed`.

## Interact with the physical endpoint

1. **Read the LED state** — `Led/State` shows the current state of the
   board's built-in LED (`LED_BUILTIN`, marked `L` on the board).
2. **Toggle the LED** — execute the `Led/Toggle` Command. The physical LED
   toggles, and the displayed `Led/State` updates after the authoritative
   readback. Write `Led/State` directly for an endpoint-confirmed Property
   write.
3. **Read the analog voltage** — `Analog Input Voltage` reports pin `A0` in
   volts. With the potentiometer wired as described above, turn the knob
   and watch the value follow between 0 and 5 V. Without it, jumper `A0` to
   `GND` for 0 V or to `5V` for about 5 V; an unconnected pin floats and
   shows an arbitrary drifting value.
4. **Press the button** — with the optional wiring in place, each press
   produces one `Controller/ButtonPressed` occurrence in the Event feed,
   attributed to `arduino-uno-01`. Events are delivered live and are not
   replayed.
5. **Unplug and replug** — disconnect the USB cable: the endpoint leaves
   `Ready` and the host begins bounded reconnection attempts. Replug it:
   the endpoint is re-verified, resynchronized, and returns to `Ready` with
   its cached values refreshed. This is the same supervision that governs
   every HASE endpoint.

## Shut down

Close the Client, then the Runtime Host, as in Example 0.

## Troubleshooting

- **`arduino-uno-01` never becomes `Ready`** — check, in order: the board
  is connected and flashed; no Arduino IDE or serial monitor holds the COM
  port; the vendor/product IDs in `desktop-runtime-endpoints.json` match
  the board (Device Manager, converted to decimal).
- **Upload fails in the Arduino IDE** — verify the selected board and port;
  close the HASE Runtime Host, which otherwise holds the port after a
  successful attachment.
- **Upload fails with `not in sync` although board and port are correct** —
  make sure nothing is wired to the `RESET` pin or to pins `0` and `1`
  (`RX`/`TX`) during the upload. A held `RESET` prevents the bootloader
  from running, and anything on the serial pins disturbs the upload
  handshake.
- **The endpoint appears, then faults after re-flashing** — flashing resets
  the board while the host is attached. The host recovers on its own; or
  press `Refresh` after the upload completes.
- **`Analog Input Voltage` shows a changing value** — a floating `A0` pin
  is expected behavior, not a defect.

## Where to go next

[Example 2 — ESP32 in the local network](Example-2-ESP32.md) continues the
ladder with a Wi-Fi instrument. The
[Arduino Uno Compact Endpoint How-To](../Arduino-Uno-Compact-Endpoint-How-To.md)
explains how to author your own compact endpoints — more Properties,
Commands, Events, and multiple boards.
