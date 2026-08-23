# HASE Example 4 — A second Runtime Host

This example completes the onboarding ladder: a second Runtime Host joins,
and the Client holds **two** authenticated sessions in one window — every
endpoint attributed to its host, every session with its own independent
connect, disconnect, and recovery. Provisioning is a repeat of
[Example 3](Example-3-Client-on-a-Second-PC.md) with distinct values; the
new ground is on the client side.

## Choosing the second host machine

Any of these works:

- a **third PC**, set up per [Getting Started](../Getting-Started.md); or
- the **client PC itself** — a Runtime Host and the Client run side by
  side on one machine, and the Client connects to the local host through
  the same mutual-TLS path as to any remote one. A two-PC household can
  complete the whole ladder this way.

Each machine runs at most one Runtime Host.

## Prerequisites

- A completed Example 3: host 1 provisioned and the client connecting.
- The second host machine with the repository cloned and built.
- A stable address for the second host machine and a distinct port — this
  example uses `52211`.

## Step 1 — Run the wizard on the second host machine

On the **second host machine**, from the repository root. Every value
differs from Example 3: the address is this machine's own, the port,
identity, profile id, and display name are new, and the output goes to a
fresh `Secured-Host2` folder so nothing from Example 3 is touched:

```powershell
$ErrorActionPreference = "Stop"

$hostIp = "192.168.0.60"

& ".\tools\Setup\Start-HaseSetup.ps1" `
    -ListenerAddress $hostIp `
    -Port 52211 `
    -RuntimeHostId "hase-example-host-02" `
    -ProfileId "example-host-2" `
    -DisplayName "Example Host 2 (secured)" `
    -OutputDirectory (Join-Path $env:LocalAppData "HASE\Secured-Host2")
```

(Execution-policy note: see Example 3, Step 2.) The default endpoint
composition expects the Example 1 Arduino; an absent configured endpoint
is tolerated at startup with a warning, so the second host publishes at
least its simulation endpoint either way. Edit
`Secured-Host2\desktop-runtime-endpoints.json` for this machine's real
instruments.

## Step 2 — Transfer four files to the client PC

Move the four transfer files from `%LocalAppData%\HASE\Secured-Host2` on
the second host machine into a **new** folder on the client PC —
`%LocalAppData%\HASE\Secured-2` — so they cannot collide with Example 3's
files of the same names:

```text
laptop-private-network.json
laptop-client.pfx
runtime-host-server.cer
client-handoff.json
```

Same rules as always: controlled channel, password separately, delete the
`.pfx` after installation. If the second host machine *is* the client PC,
this is a local copy between the two folders.

## Step 3 — Run the wizard client role against the new bundle

On the **client PC**, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\tools\Setup\Start-HaseSetup.ps1" `
    -BundleDirectory (Join-Path $env:LocalAppData "HASE\Secured-2")
```

This installs the second host's credential and pinned certificate. It also
writes a single-host registry beside the bundle — ignore it; the next step
merges the second host into your existing registry instead.

## Step 4 — Add the second host to your registry

The repository's registry tool appends a host to an existing registry with
an automatic backup. It refuses while the Client runs, so **close the
Client first**. On the client PC, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.Client.RegistryTool\bin\Release\net10.0\Hase.Client.RegistryTool.exe" add `
    (Join-Path $env:LocalAppData "HASE\Secured\client-runtime-hosts.json") `
    example-host-2 `
    "Example Host 2 (secured)" `
    hase-example-host-02 `
    (Join-Path $env:LocalAppData "HASE\Secured-2\laptop-private-network.json") `
    true
```

The values are exactly those chosen in Step 1 (they are also readable in
`Secured-2\client-handoff.json`). Expected output: the operation succeeds,
names the profile id, and reports the backup path of the previous
registry.

## Step 5 — Allow the port on the second host machine

As in Example 3 Step 5 — elevated window, then verify:

```powershell
New-NetFirewallRule -DisplayName "HASE Runtime Host 2 (secured)" `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort 52211 `
    -Profile Private
```

## Step 6 — Start everything

Start host 1 as in Example 3. On the **second host machine**, from the
repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Secured-Host2\desktop-runtime-host.json")
```

Start the Client as in Example 3 — same command, same registry; it now
lists **two** runtime hosts: `Example Host (secured)` and
`Example Host 2 (secured)`.

## Operate two hosts in one window

1. **Connect both** — each entry has its own `Connect`; both tiles turn
   green independently. Selecting a host shows *its* endpoints; both hosts
   publish a `simulation-byte-buffer-validation` endpoint, and the
   selection makes unambiguous which host's simulation you are operating.
2. **Work across hosts** — read, write, execute, and observe on one host,
   then switch the selection and do the same on the other. Every
   operation, Event, and diagnostic is attributed to exactly one host.
3. **Prove independence** — close the second host's window. Its tile
   leaves `Connected` while the first host's session, values, and Events
   continue untouched. Start the second host again and press `Connect` on
   its entry: the session re-establishes without restarting the Client.

## Troubleshooting

Everything from
[Example 3's troubleshooting](Example-3-Client-on-a-Second-PC.md#troubleshooting)
applies per host — run the `Test-NetConnection` diagnosis against the
second host's address and port. Specific to this example:

- **The registry tool refuses** — the Client is still running, or the
  profile id already exists in the registry (each host needs a unique
  profile id and identity).
- **Both hosts on one entry / mixed endpoints** — the registry was not
  merged; check that Step 4 targeted `Secured\client-runtime-hosts.json`
  and the Client was started with that same file.

## The ladder is complete

Simulation on one PC, a USB instrument, a Wi-Fi instrument, a secured
remote client, and a multi-host laboratory — that is the complete HASE
onboarding ladder. From here:

- author your own endpoints with the
  [Arduino Uno How-To](../Arduino-Uno-Compact-Endpoint-How-To.md) and the
  [ESP32 Endpoint Authoring Guide](../ESP32-Endpoint-Authoring-Guide.md);
- automate your setup with the
  [Python client](../../python/hase-client/README.md); and
- go deeper with the
  [Northbound API Reference](../API%20reference/HASE-Northbound-API-Reference.md).
