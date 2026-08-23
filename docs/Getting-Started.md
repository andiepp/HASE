# HASE Getting Started

This guide takes you from a fresh clone to a running HASE system on one
Windows PC: a Runtime Host publishing a simulated endpoint, and the HASE
Client reading and writing live values against it. This is **Example 0** of
the onboarding ladder — it needs no hardware, no network configuration, and
no certificates.

The example runs on the explicitly labeled **development loopback profile**:
the Runtime Host binds its northbound gRPC API to a loopback address only,
without TLS and without client certificates, and refuses every non-loopback
address. This profile exists for single-PC development and evaluation.
Every deployment that leaves loopback — a client on a second PC, a host on
the network — requires the secured mutual-TLS configuration instead
(Examples 3 and 4).

## Prerequisites

- Windows 10 or 11.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
  Verify with `dotnet --version`; any 10.0.3xx or later SDK is sufficient.
- [Git](https://git-scm.com/downloads).

Visual Studio is not required. No hardware, toolchain, Python, or
certificate is needed for this example.

All commands below run in PowerShell.

## Clone and build

```powershell
git clone https://github.com/andiepp/HASE.git
cd HASE
dotnet build .\HASE.slnx -c Release
```

The build must end with `0 errors`. A number of compiler warnings is the
accepted baseline of the current code and is expected.

Optionally, run the complete automated test suite (about two minutes):

```powershell
dotnet test .\HASE.slnx -c Release
```

Every test project must pass with zero failures and zero skips.

## Example 0 — Simulation on one PC

Four small JSON files describe the system: the Runtime Host's identity, the
Runtime Host's development profile, the Client's connection configuration,
and the Client's runtime-host registry. The following block writes all four
to a local folder outside the repository. Run it from anywhere; it does not
depend on the repository location.

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$hase = Join-Path $env:LocalAppData "HASE\Development"
New-Item -ItemType Directory -Force $hase | Out-Null

@"
{
  "formatVersion": 1,
  "runtimeHostId": "hase-development-host-01"
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "runtime-host-identity.json")

@"
{
  "formatVersion": 1,
  "profileKind": "development-loopback",
  "identityFilePath": "$($hase.Replace('\', '\\'))\\runtime-host-identity.json",
  "loopbackAddress": "127.0.0.1",
  "port": 52110,
  "includeByteBufferSimulation": true
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "desktop-runtime-development.json")

@"
{
  "formatVersion": 1,
  "profileKind": "development-loopback",
  "address": "http://127.0.0.1:52110"
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "client-development.json")

@"
{
  "formatVersion": 1,
  "hosts": [
    {
      "profileId": "development-loopback",
      "displayName": "Development (loopback, no TLS)",
      "expectedRuntimeHostId": "hase-development-host-01",
      "enabled": true,
      "privateNetworkConfigurationFilePath": "$($hase.Replace('\', '\\'))\\client-development.json"
    }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $hase "client-runtime-hosts.json")

Get-ChildItem $hase
```

Expected result: the listing shows the four files in
`%LocalAppData%\HASE\Development`.

Notes:

- The identity file fixes the Runtime Host's stable identity before its
  first start, so the Client's expected-identity check matches
  deterministically.
- The port `52110` is arbitrary. If it is taken on your machine, choose
  another and change it in **both** `desktop-runtime-development.json` and
  `client-development.json`.

### Start the Runtime Host

From the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    --development (Join-Path $env:LocalAppData "HASE\Development\desktop-runtime-development.json")
```

Expected result: the HASE Runtime Host window opens and shows

- the composition `DEVELOPMENT loopback runtime host - no TLS, no client
  certificates`;
- the binding `http://127.0.0.1:52110 (DEVELOPMENT - loopback only, no
  TLS)`; and
- one endpoint, `simulation-byte-buffer-validation`, in state `Ready`.

`Open Diagnostics` additionally shows a `DevelopmentLoopbackHostingActive`
warning record — the deliberate reminder that this process runs without
transport security.

### Start the Client

Leave the Runtime Host running. In a second PowerShell window, from the
repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.Client.Wpf.App\bin\Release\net10.0-windows\Hase.Client.Wpf.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Development\client-runtime-hosts.json")
```

Expected result: the HASE Client window opens and its runtime-host list
shows one entry, `Development (loopback, no TLS)`.

Press `Connect` on that entry. The tile turns green, and the endpoint
column shows `simulation-byte-buffer-validation`.

### Interact with the simulated endpoint

Select the endpoint. The Properties and Commands column shows the
simulation's instrument with its Properties, Commands, and Events. Try:

1. **Boolean write** — edit the `Enabled` Property and apply it. The
   displayed value updates after the endpoint confirms the write.
2. **Numeric write** — set a new `Setpoint` value.
3. **String write** — change the `Label` text.
4. **Events** — execute one of the `Emit` Commands (for example the
   Boolean-event Command). The Event feed shows the occurrence with the
   endpoint and instrument attribution.

Every value you see travels the same path a physical instrument uses:
Client → gRPC over loopback HTTP/2 → Runtime Host → normalized runtime
model → simulated endpoint, and back.

### Shut down

Close the Client window, then close the Runtime Host window. Both shut
down orderly; no background processes remain.

## Troubleshooting

- **`dotnet` is not recognized** — the .NET 10 SDK is not installed or not
  on `PATH`. Install it and reopen PowerShell.
- **The executable path is not found** — the solution was not built in the
  `Release` configuration, or the command was not run from the repository
  root. Run `dotnet build .\HASE.slnx -c Release` first.
- **The Runtime Host refuses the configuration** — the error message names
  the offending value. A non-loopback `loopbackAddress` is refused by
  design; the development profile never binds beyond loopback.
- **"HASE Runtime Host is already running" / "HASE Client is already
  running"** — each application allows one instance. Close the existing
  window first.
- **The Client entry stays disconnected after `Connect`** — verify the
  Runtime Host window is open and shows `Ready`, and that the port in
  `client-development.json` matches `desktop-runtime-development.json`.
- **The port is in use** — choose a different port in both files and
  restart both applications.

## Where to go next

Example 0 is the first step of the onboarding ladder:

```text
Example 0  Simulation only (this guide)
Example 1  Arduino Uno on the same PC over USB serial
Example 2  ESP32 in the local network
Example 3  Client on a second PC through guided mutual-TLS provisioning
Example 4  A second Runtime Host and the multi-host Client
```

Example 1 is published:
[Arduino Uno on one PC](examples/Example-1-Arduino-Uno.md) continues directly from
this guide with a physical USB instrument, and
[Example 2 — ESP32 in the local network](examples/Example-2-ESP32.md) follows with a
Wi-Fi instrument, and
[Example 3 — Client on a second PC](examples/Example-3-Client-on-a-Second-PC.md)
crosses the network under mutual TLS, and
[Example 4 — A second Runtime Host](examples/Example-4-Second-Runtime-Host.md)
completes the ladder with the multi-host Client. The
[Laptop Client UI Tutorial](Tutorial/HASE-Laptop-Client-UI-Tutorial.md)
and the [Northbound API Reference](API%20reference/HASE-Northbound-API-Reference.md)
describe the Client and the API in depth.
