# HASE Example 3 — Client on a second PC

This example separates the Client from the Runtime Host: two PCs, one
private network, and the full HASE security boundary between them — mutual
TLS, a pinned server certificate, certificate-to-principal enrollment, and
per-operation authorization. The guided setup wizard performs the entire
provisioning; you answer three questions and move four files.

[Two-Computer Provisioning](Provisioning-Two-Computers.md) explains the
security model and every document the wizard writes; this example is the
runnable path through it.

## Prerequisites

- Two Windows 10/11 PCs on the same private network, each with the
  repository cloned and built in `Release`
  (see [Getting Started](Getting-Started.md)).
- A stable address for the host PC (a DHCP reservation in your router) and
  one chosen TCP port (this example uses `52210`).
- Ideally, an instrument from [Example 1](Example-1-Arduino-Uno.md) or
  [Example 2](Example-2-ESP32.md) attached to the host PC — the simulation
  endpoint works with no hardware.
- A secure way to move four files between the PCs, and a separate channel
  for one password.

## Step 1 — Run the wizard on the host PC

From the repository root, with your host PC's address:

```powershell
$ErrorActionPreference = "Stop"
& ".\tools\Setup\Start-HaseSetup.ps1" `
    -ListenerAddress "192.168.0.50" `
    -Port 52210 `
    -OutputDirectory (Join-Path $env:LocalAppData "HASE\Secured")
```

The wizard prompts for a transfer password, creates the certificates and
every host document in `%LocalAppData%\HASE\Secured`, and prints the next
steps. Its default endpoint composition expects the Example 1 Arduino;
edit `desktop-runtime-endpoints.json` in the output folder for your own
endpoint mix (the entries are exactly those of Examples 1 and 2) before
starting the host.

## Step 2 — Transfer four files

Move these files from `%LocalAppData%\HASE\Secured` on the host PC to
`%LocalAppData%\HASE\Secured` on the client PC:

```text
laptop-private-network.json
laptop-client.pfx
runtime-host-server.cer
client-handoff.json
```

Use a channel you control, and pass the transfer password separately —
never beside the files.

## Step 3 — Run the wizard on the client PC

From the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\tools\Setup\Start-HaseSetup.ps1" `
    -BundleDirectory (Join-Path $env:LocalAppData "HASE\Secured")
```

The wizard prompts for the same transfer password, installs the client
credential (non-exportable) and the pinned server certificate, and writes
the client registry. Then securely delete `laptop-client.pfx` from every
transfer location — the credential lives only in the certificate store
now.

## Step 4 — Allow the port on the host PC

In an **elevated** PowerShell on the host PC:

```powershell
New-NetFirewallRule -DisplayName "HASE Runtime Host (secured)" `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort 52210 `
    -Profile Private
```

## Step 5 — Start and connect

On the host PC, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Secured\desktop-runtime-host.json")
```

Expected result: the composition reads `Production private-network runtime
host`, the binding is HTTPS, and the configured endpoints publish as in
the earlier examples.

On the client PC, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.Client.Wpf.App\bin\Release\net10.0-windows\Hase.Client.Wpf.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Secured\client-runtime-hosts.json")
```

Press `Connect` on `Example Host (secured)`. The TLS handshake pins the
server certificate, the host authenticates and enrolls your client
certificate, the Client verifies the host's authoritative identity — and
the tile turns green with the host's endpoints listed.

## Interact across the network

Everything from Examples 0 through 2 works identically — reads, writes,
Commands, and live Events — except that every operation now crosses the
network authenticated and encrypted, and only the operations granted to
your client principal succeed:

1. Read and write Properties, execute Commands, and watch Events exactly
   as before; the UI is unchanged.
2. **Interrupt the network** — disconnect the client PC's network briefly.
   The entry leaves `Connected` while the Runtime Host and its instruments
   keep running untouched. When the network returns, reconnect through the
   entry's `Connect` control.
3. **See authorization work (optional)** — on the host PC, close the
   Runtime Host, remove the `property.write` grant line from
   `%LocalAppData%\HASE\Secured\authorization-policy.json`, and start the
   host again. Reads, Commands, and Events still work from the client, but
   every Property write is now denied — the same certificate, one grant
   less. Restore the line and restart to return to full access.

## Shut down

Close the Client, then the Runtime Host. The provisioned configuration is
persistent: the next start of both applications needs only Step 5.

## Troubleshooting

The security boundary fails closed by design — a missing certificate, a
wrong pin, an unenrolled credential, or a malformed document stops the
affected side with an error rather than degrading. The
[Two-Computer Provisioning](Provisioning-Two-Computers.md) failure section
lists the causes; the most common first-run issues are the firewall rule
(Step 4), a wrong host address, and PCs on networks that isolate clients
from each other.

## Where to go next

Example 4 (a second Runtime Host and the multi-host Client) is in
preparation. The
[Northbound API Reference](API%20reference/HASE-Northbound-API-Reference.md)
documents the API you are now using remotely, and the
[Laptop Client UI Tutorial](Tutorial/HASE-Laptop-Client-UI-Tutorial.md)
covers the Client in depth.
