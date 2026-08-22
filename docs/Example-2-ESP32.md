# HASE Example 2 — ESP32 in the local network

This example adds the first network instrument: an ESP32 board with a
BME280 environment sensor, joined to your Wi-Fi and attached by the
development Runtime Host over native HASE Protocol Version 1 on framed TCP.
The instrument leaves the USB cable; the Runtime Host and Client still run
on one PC on the certificate-free loopback development profile from
[Example 0](Getting-Started.md).

Security note: the ESP32 endpoint speaks unauthenticated Protocol Version 1
to anyone on your local network — appropriate for a trusted home or lab
network, not for shared or public networks. The northbound API stays
loopback-only; putting the *Client* on a second PC is Example 3, where
mutual TLS begins.

## Parts list

- One ESP32 development board of the DOIT ESP32 DevKit family (the
  validated board is the DOIT ESP32 DevKitC V4; any ESP32 dev board works
  if pins 16, 17, 21, and 22 are free), with its USB cable.
- One BME280 sensor breakout (3.3 V, I2C) and four jumper wires. The BME280
  is **required**: the firmware stops before starting the network endpoint
  when the sensor is not found.
- Optional, for the Event step: one push button and two jumper wires.
- Optional, for the LED steps: one LED and one series resistor (about
  220 Ohm), or just a multimeter/jumper — the status LED pin works without
  anything connected; you simply won't see it.
- A 2.4 GHz Wi-Fi network. The ESP32 does not support 5 GHz.

## Prerequisites

- A completed Example 0. [Example 1](Example-1-Arduino-Uno.md) is
  recommended but not required.
- The Arduino IDE (2.x; the repository validates 2.3.7) with ESP32 board
  support: *Tools → Board → Boards Manager*, search `esp32`, install
  **esp32 by Espressif Systems** (the repository validates core 3.3.10).

## Install the libraries

The HASE endpoint library and the exact validated Adafruit sensor libraries
are part of the repository — nothing is downloaded from the Library
Manager. Close the Arduino IDE and run this block from the repository root:

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$sketchbookLibraries = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "Arduino\libraries"
New-Item -ItemType Directory -Force $sketchbookLibraries | Out-Null

Copy-Item -Recurse -Force ".\libraries\HaseEsp32Endpoint" $sketchbookLibraries
Copy-Item -Recurse -Force ".\HaseEndpoint\Libraries\Adafruit_BME280_Library" $sketchbookLibraries
Copy-Item -Recurse -Force ".\HaseEndpoint\Libraries\Adafruit_BMP280_Library" $sketchbookLibraries
Copy-Item -Recurse -Force ".\HaseEndpoint\Libraries\Adafruit_BusIO" $sketchbookLibraries
Copy-Item -Recurse -Force ".\HaseEndpoint\Libraries\Adafruit_Unified_Sensor" $sketchbookLibraries

Get-ChildItem $sketchbookLibraries -Directory | Select-Object Name
```

If your sketchbook already contains other versions of the Adafruit
libraries, this replaces them with the repository-validated versions
(BME280 2.3.0, BMP280 3.0.0, BusIO 1.17.4, Unified Sensor 1.1.15).

## Wire the hardware

| BME280 pin | ESP32 pin |
| --- | --- |
| VIN / VCC | 3V3 |
| GND | GND |
| SCL | GPIO 22 |
| SDA | GPIO 21 |

The firmware expects the BME280 at I2C address `0x76` (the common breakout
default; boards with the alternate `0x77` address usually have a solder
jumper to select `0x76`).

Optional: push button between `GPIO 17` and `GND` (active-low with the
internal pull-up, 50 ms debounce). Optional: LED with its series resistor
from `GPIO 16` to `GND` to see the status LED Property and Command.

## Create the Wi-Fi secrets

The firmware reads your Wi-Fi credentials from `HaseEndpoint\HaseSecrets.h`,
which is listed in `.gitignore` and never committed. Create it from the
tracked template, then put your real SSID and password in:

```powershell
$ErrorActionPreference = "Stop"
Copy-Item ".\templates\HaseEndpoint\HaseSecrets.example.h" ".\HaseEndpoint\HaseSecrets.h"
notepad ".\HaseEndpoint\HaseSecrets.h"
```

Replace `TEST_WIFI_SSID` and `TEST_WIFI_PASSWORD` with your network's
values and save. Keep credentials out of every commit; `git status` must
never show `HaseSecrets.h`.

## Flash and observe

1. Connect the ESP32 over USB.
2. In the Arduino IDE, open `HaseEndpoint\HaseEndpoint.ino` from your
   clone.
3. Select *Tools → Board → esp32 → DOIT ESP32 DEVKIT V1* and the board's
   COM port.
4. Press *Upload*. Some boards require holding the `BOOT` button when the
   IDE prints `Connecting...`.
5. Open the Serial Monitor at 115200 baud and press the board's `EN`
   (reset) button.

Unlike the Arduino Uno, the ESP32's USB serial line carries only diagnostic
text — the HASE transport is TCP. The expected startup sequence:

```text
HASE ESP32 Endpoint

Endpoint ID   : doit-esp32-devkitc-v4-01
Initializing BME280 environment sensor...
BME280 initialized.
...
Connecting to Wi-Fi...
Wi-Fi connected.
Synchronizing UTC clock...
UTC clock synchronized.
HASE network endpoint advertised through mDNS/DNS-SD.
```

`BME280 initialization failed.` means the wiring or the I2C address is
wrong; the endpoint deliberately does not start without its sensor.

## Determine the board's address

The board advertises itself via mDNS as `doit-esp32-devkitc-v4-01`. Test
whether your network resolves it:

```powershell
ping doit-esp32-devkitc-v4-01.local
```

If the name resolves, use `doit-esp32-devkitc-v4-01.local` as the host
below. If not, find the board's IP address in your router's device list
and use that instead — and consider giving the board a DHCP reservation so
the address survives restarts.

## Add the endpoint to the development Runtime Host

Both applications closed, run this block. It writes the composition with
the ESP32 endpoint; replace the host value if you use an IP address. If you
completed Example 1, keep the Arduino by leaving its `CompactSerial` object
in the `endpoints` array exactly as written there — the array accepts both.

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$hase = Join-Path $env:LocalAppData "HASE\Development"

@"
{
  "formatVersion": 1,
  "endpoints": [
    {
      "kind": "NativeNetwork",
      "expectedEndpointId": "doit-esp32-devkitc-v4-01",
      "host": "doit-esp32-devkitc-v4-01.local",
      "port": 5000
    }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "desktop-runtime-endpoints.json")
```

The development host profile from Example 1 already references this file.
If you skipped Example 1, add the `endpointCompositionFilePath` line to
`desktop-runtime-development.json` as shown there.

## Start and verify

Start the Runtime Host and Client exactly as in Example 0. Expected result:
`doit-esp32-devkitc-v4-01` is published and `Ready` next to the simulation
endpoint (and the Arduino, if configured and connected). A configured
endpoint that is unreachable at startup is tolerated and reported as a
warning; fix its connectivity and press `Refresh` to attach it without a
restart.

Selecting the ESP32 endpoint shows two instruments:

- the BME280 environment sensor with the read-only Properties
  `Temperature`, `Relative Humidity`, and `Air Pressure`; and
- the GPIO controller with the writable `Status LED Enabled` Property, the
  `Toggle Status LED` Command, and the `Button Pressed` Event.

## Interact with the network endpoint

1. **Read the environment** — Temperature, Relative Humidity, and Air
   Pressure show live BME280 values. Warm the sensor with a fingertip and
   watch Temperature rise.
2. **Switch the LED** — write `Status LED Enabled` or execute
   `Toggle Status LED`; with the optional LED wired to `GPIO 16` you see
   it, and the read-back state updates either way.
3. **Press the button** — each press of the `GPIO 17` button produces one
   `Button Pressed` occurrence in the Event feed, attributed to
   `doit-esp32-devkitc-v4-01`.
4. **Interrupt the network** — press the board's `EN` (reset) button: the
   endpoint leaves `Ready` and the host begins bounded reconnection. When
   the board has rebooted and rejoined Wi-Fi, the endpoint is re-verified,
   resynchronized, and returns to `Ready`. The same recovery covers Wi-Fi
   loss.

## Shut down

Close the Client, then the Runtime Host. The ESP32 keeps running and
advertising independently; unplug it or leave it — the next host start
attaches it again.

## Troubleshooting

- **`BME280 initialization failed.`** — check the four wires and the I2C
  address; the sensor must be at `0x76`.
- **Wi-Fi never connects (dots forever)** — verify SSID and password in
  `HaseEndpoint\HaseSecrets.h`, and that the network is 2.4 GHz.
- **`ping doit-esp32-devkitc-v4-01.local` fails** — some networks block or
  don't forward mDNS. Use the IP address from your router instead.
- **The endpoint never becomes `Ready`** — confirm the serial monitor shows
  the full startup sequence; confirm the PC and the ESP32 are on the same
  network; some guest networks isolate clients from each other.
- **Compile errors about missing libraries** — re-run the library-copy
  block with the Arduino IDE closed, then restart the IDE.
- **Upload fails with `Connecting...`** — hold the board's `BOOT` button
  until the upload starts.

## Where to go next

Example 3 (Client on a second PC through guided mutual-TLS provisioning) is
in preparation. The
[ESP32 Endpoint Authoring Guide](ESP32-Endpoint-Authoring-Guide.md)
explains how to author your own ESP32 endpoints on the same library.
