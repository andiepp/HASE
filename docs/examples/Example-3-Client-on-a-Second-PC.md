# HASE Example 3 — Client on a second PC

This example separates the Client from the Runtime Host: two PCs, one
private network, and the full HASE security boundary between them — mutual
TLS, a pinned server certificate, certificate-to-principal enrollment, and
per-operation authorization. The guided setup wizard performs the entire
provisioning; you answer three questions and move four files.

[Two-Computer Provisioning](../Provisioning-Two-Computers.md) explains the
security model and every document the wizard writes; this example is the
runnable path through it.

## Prerequisites

- Two Windows 10/11 PCs on the same private network.
- A stable address for the host PC (a DHCP reservation in your router) and
  one chosen TCP port (this example uses `52210`).
- Ideally, an instrument from [Example 1](Example-1-Arduino-Uno.md) or
  [Example 2](Example-2-ESP32.md) attached to the host PC — the simulation
  endpoint works with no hardware.
- A secure way to move four files between the PCs, and a separate channel
  for one password.

## Step 1 — Set up HASE on both PCs

HASE runs from a built clone; there is no installer. **Each of the two
PCs** needs the repository cloned and built — the host PC from the earlier
examples already qualifies, and the client PC needs the same setup now:

```powershell
git clone https://github.com/andiepp/HASE.git
cd HASE
dotnet build .\HASE.slnx -c Release
```

The prerequisites (Windows, .NET 10 SDK, Git) and the expected build
result are described in [Getting Started](../Getting-Started.md). Every later
step states which PC it runs on; "from the repository root" always means
that PC's own clone.

## Step 2 — Run the wizard on the host PC

A note before the first script: Windows PowerShell's default execution
policy blocks `.ps1` files. If the wizard is refused with a
`running scripts is disabled` error, allow scripts for the current session
first:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

(or durably for your account with `-Scope CurrentUser -ExecutionPolicy
RemoteSigned`). The same applies on the client PC in Step 4.

On the **host PC**, from the repository root. Set `$hostIp` to the host
PC's actual address on your network (shown by `ipconfig`) — the wizard
bakes it into the server certificate and the client's pinned
configuration:

```powershell
$ErrorActionPreference = "Stop"

$hostIp = "192.168.0.50"

& ".\tools\Setup\Start-HaseSetup.ps1" `
    -ListenerAddress $hostIp `
    -Port 52210 `
    -OutputDirectory (Join-Path $env:LocalAppData "HASE\Secured")
```

The wizard prompts for a transfer password, creates the certificates and
every host document in `%LocalAppData%\HASE\Secured`, and prints the next
steps. Its default endpoint composition expects the Example 1 Arduino;
edit `desktop-runtime-endpoints.json` in the output folder for your own
endpoint mix (the entries are exactly those of Examples 1 and 2) before
starting the host.

## Step 3 — Transfer four files

Move these files from `%LocalAppData%\HASE\Secured` on the host PC to
`%LocalAppData%\HASE\Secured` on the client PC:

```text
laptop-private-network.json
laptop-client.pfx
runtime-host-server.cer
client-handoff.json
```

Use a channel you control, and pass the transfer password separately —
never beside the files. Place the files exactly in
`%LocalAppData%\HASE\Secured` (create the folder if needed) — the wizard
and every later command reference that exact path.

## Step 4 — Run the wizard on the client PC

On the **client PC**, from the repository root:

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

## Step 5 — Allow the port on the host PC

Creating a firewall rule requires an **elevated** PowerShell: right-click
the Start button and choose *Terminal (Admin)* or *Windows PowerShell
(Administrator)*. In a normal window the command fails with
`access denied` — and then the rule does **not** exist. On the host PC,
elevated:

```powershell
New-NetFirewallRule -DisplayName "HASE Runtime Host (secured)" `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort 52210 `
    -Profile Private
```

Verify it in the same elevated window — the output must show
`Enabled: True` and `Profile: Private`:

```powershell
Get-NetFirewallRule -DisplayName "HASE Runtime Host (secured)" | Select-Object DisplayName, Enabled, Profile
```

The rule is scoped to the `Private` firewall profile. If Windows
classifies your network as `Public`, the rule does not apply; check with
`Get-NetConnectionProfile` and recategorize with
`Set-NetConnectionProfile -NetworkCategory Private` (elevated) if needed.

## Step 6 — Start and connect

On the **host PC**, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Secured\desktop-runtime-host.json")
```

Expected result: the composition reads `Production private-network runtime
host`, the binding is HTTPS, and the configured endpoints publish as in
the earlier examples.

On the **client PC**, from the repository root:

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
persistent: the next start of both applications needs only Step 6.

## Troubleshooting

First, read the symptom. A client stuck in `Connecting` means packets are
not getting through — a network or firewall problem. A fast failure with
an error means the connection arrived but a security check refused it —
the boundary fails closed by design, and the
[Two-Computer Provisioning](../Provisioning-Two-Computers.md) failure section
lists those causes.

For the stuck-`Connecting` case, diagnose from the **client PC**:

```powershell
Test-NetConnection 192.168.0.50 -Port 52210
```

(with your host address). Interpret the result:

- **`PingSucceeded: False`** — the PCs cannot reach each other: wrong
  address, different networks, or a network that isolates clients (guest
  Wi-Fi).
- **`PingSucceeded: True`, `TcpTestSucceeded: False`** — the network is
  fine and the host PC's firewall is blocking the port. Verify the rule
  and the network category as described in Step 5; the most common causes
  are a rule that was never created because the command ran without
  elevation, and a network Windows classified as `Public`.
- **`TcpTestSucceeded: True`** — reachability is fine; if `Connect` still
  fails, the error now names a security cause: a wrong `$hostIp` baked
  into the certificate, files from different provisioning runs mixed
  together (re-run the wizard into a fresh directory on both sides), or an
  `expectedRuntimeHostId` mismatch.

Further hints:

- **`running scripts is disabled`** — the execution-policy note in
  Step 2.
- **The wizard reports a missing file** — the four transferred files are
  not exactly in `%LocalAppData%\HASE\Secured` (Step 3).
- **The host window shows `Identity: hase-desktop-runtime-host`** — a
  known cosmetic display constant; the effective identity is the one the
  wizard wrote, and the Client verifies against that.
- No step requires restarting the Runtime Host after firewall or network
  changes — fix the reachability and press `Connect` again.

## Where to go next

[Example 4 — A second Runtime Host](Example-4-Second-Runtime-Host.md)
completes the ladder with the multi-host Client. The
[Northbound API Reference](../API%20reference/HASE-Northbound-API-Reference.md)
documents the API you are now using remotely, and the
[Laptop Client UI Tutorial](../Tutorial/HASE-Laptop-Client-UI-Tutorial.md)
covers the Client in depth.
