# HASE Two-Computer Provisioning

This document provisions the secured HASE configuration for two computers:
one PC running the Runtime Host with its instruments, and a second PC
running the Client over the network. It is the manual reference behind
Example 3 of the onboarding ladder and the specification for the guided
setup wizard.

Unlike the single-PC development profile of Examples 0 through 2, remote
access uses the full HASE security boundary:

- **Mutual TLS** — the host presents a server certificate the client has
  pinned byte-exactly; the client presents a client certificate the host
  validates against its trust chain.
- **Enrollment** — a valid certificate alone grants nothing; the host maps
  the exact certificate (by SHA-256) to a HASE principal through an
  explicit enrollment document.
- **Authorization** — the principal receives exactly the operations granted
  in the host's authorization policy. Network reachability never grants
  HASE authority.

What this provisioning creates is development-grade: a self-signed
deployment-specific root in the host user's certificate store, no
revocation, no rotation. It is appropriate for a trusted private network.
The production requirements (audit, revocation, rotation, governance)
remain separate, per ADR-0031.

## Placeholders

| Placeholder | Meaning | Example |
| --- | --- | --- |
| `HOST-PC` | The computer running the Runtime Host | your desktop |
| `CLIENT-PC` | The computer running the Client | your laptop |
| `<HOST-IP>` | The `HOST-PC` address on your network | `192.168.0.50` |
| `<PORT>` | One fixed TCP port for the secured API | `52210` |

Give `HOST-PC` a stable address (a DHCP reservation in your router). The
address must be a specific private address — the tooling refuses wildcard
and loopback listeners.

## Prerequisites

- Two Windows 10/11 PCs on the same private network.
- The repository cloned and built in `Release` on **both** PCs
  (see [Getting Started](Getting-Started.md)).
- A secure way to move three files from `HOST-PC` to `CLIENT-PC` (USB
  stick you control, or an encrypted channel), and a **separate** channel
  for one transfer password.

## Step 1 — Create the credential bundle on HOST-PC

On `HOST-PC`, in PowerShell from the repository root, set your values and
run the repository's provisioning script:

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$hostIp = "192.168.0.50"
$port = 52210
$secured = Join-Path $env:LocalAppData "HASE\Secured"

& ".\tools\PrivateNetwork\New-HasePrivateNetworkValidationBundle.ps1" `
    -ListenerAddress $hostIp `
    -Port $port `
    -OutputDirectory $secured
```

The script prompts for a transfer password (it protects the client
credential file; never put it on the command line). It then:

- creates a deployment-specific root certificate and installs it in the
  `HOST-PC` current-user trusted-root store;
- creates the server certificate — carrying `<HOST-IP>` as its IP subject
  alternative name — in the current-user personal store;
- creates the client certificate, exports it once as a password-protected
  `laptop-client.pfx`, and removes its private key from `HOST-PC`;
- writes `desktop-private-network.json` (the host's secured listener
  configuration), `laptop-private-network.json` (the client's connection
  configuration with the pinned server certificate), and
  `client-enrollments.json` (the certificate-to-principal enrollment for
  the principal `laptop-validation-client`).

Nothing sensitive is printed, and nothing here may ever be committed —
keep the output directory outside the repository, as configured above.

## Step 2 — Transfer three files to CLIENT-PC

Move exactly these files from `%LocalAppData%\HASE\Secured` on `HOST-PC`
to a temporary folder on `CLIENT-PC`:

```text
laptop-private-network.json
laptop-client.pfx
runtime-host-server.cer
```

Do not email the `.pfx`, put it in source control or a shared folder, or
keep unnecessary copies. Communicate the transfer password through a
separate channel.

## Step 3 — Install the client credential on CLIENT-PC

On `CLIENT-PC`, in PowerShell from the repository root (with the three
files in, for example, `%LocalAppData%\HASE\Secured`):

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$secured = Join-Path $env:LocalAppData "HASE\Secured"

& ".\tools\PrivateNetwork\Install-HasePrivateNetworkValidationClient.ps1" `
    -BundleDirectory $secured
```

The script prompts for the transfer password, imports the client
certificate (non-exportable) into the current-user personal store and the
server certificate into the current-user trusted-people store, and
verifies both against `laptop-private-network.json`. Afterward, securely
delete `laptop-client.pfx` from every transfer location.

## Step 4 — Author the host application documents on HOST-PC

The Runtime Host application needs four more documents beside the
generated ones: its identity, its endpoint composition, the authorization
policy, and the installation profile tying everything together.

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$secured = Join-Path $env:LocalAppData "HASE\Secured"

@"
{
  "formatVersion": 1,
  "runtimeHostId": "hase-example-host-01"
}
"@ | Set-Content -Encoding utf8 (Join-Path $secured "runtime-host-identity.json")

@"
{
  "formatVersion": 1,
  "grants": [
    { "principalId": "laptop-validation-client", "permission": "runtime-host.snapshot.read" },
    { "principalId": "laptop-validation-client", "permission": "property.cached.read" },
    { "principalId": "laptop-validation-client", "permission": "property.authoritative.read" },
    { "principalId": "laptop-validation-client", "permission": "property.write" },
    { "principalId": "laptop-validation-client", "permission": "command.execute" },
    { "principalId": "laptop-validation-client", "permission": "observation.subscribe" }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $secured "authorization-policy.json")

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
"@ | Set-Content -Encoding utf8 (Join-Path $secured "desktop-runtime-endpoints.json")

@"
{
  "formatVersion": 1,
  "identityFilePath": "$($secured.Replace('\', '\\'))\\runtime-host-identity.json",
  "privateNetworkConfigurationFilePath": "$($secured.Replace('\', '\\'))\\desktop-private-network.json",
  "endpointCompositionFilePath": "$($secured.Replace('\', '\\'))\\desktop-runtime-endpoints.json",
  "authorizationPolicyFilePath": "$($secured.Replace('\', '\\'))\\authorization-policy.json",
  "includeByteBufferSimulation": true
}
"@ | Set-Content -Encoding utf8 (Join-Path $secured "desktop-runtime-host.json")

Get-ChildItem $secured
```

Notes:

- The endpoint composition above carries the Example 1 Arduino; replace
  the `endpoints` array with your own mix of `CompactSerial` and
  `NativeNetwork` entries from Examples 1 and 2, and keep
  `includeByteBufferSimulation` for a hardware-free start.
- The policy grants the client principal the six operational permissions.
  Remote diagnostics (`diagnostics.subscribe`) is deliberately absent.

## Step 5 — Author the client registry on CLIENT-PC

```powershell
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$secured = Join-Path $env:LocalAppData "HASE\Secured"

@"
{
  "formatVersion": 1,
  "hosts": [
    {
      "profileId": "example-host",
      "displayName": "Example Host (secured)",
      "expectedRuntimeHostId": "hase-example-host-01",
      "enabled": true,
      "privateNetworkConfigurationFilePath": "$($secured.Replace('\', '\\'))\\laptop-private-network.json"
    }
  ]
}
"@ | Set-Content -Encoding utf8 (Join-Path $secured "client-runtime-hosts.json")
```

The `expectedRuntimeHostId` must match the identity authored in Step 4;
the Client verifies it against the host's authoritative snapshot after the
TLS and enrollment checks.

## Step 6 — Allow the port through the HOST-PC firewall

Windows Defender Firewall blocks inbound connections by default. On
`HOST-PC`, in an **elevated** PowerShell, allow exactly the chosen port:

```powershell
New-NetFirewallRule -DisplayName "HASE Runtime Host (secured)" `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort 52210 `
    -Profile Private
```

Use your `<PORT>` value, and keep the rule scoped to the `Private`
firewall profile.

## Step 7 — Start and connect

On `HOST-PC`, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.DesktopHost.App\bin\Release\net10.0-windows\Hase.DesktopHost.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Secured\desktop-runtime-host.json")
```

The window shows the composition `Production private-network runtime host`
with the HTTPS binding, and the configured endpoints publish as in the
examples.

On `CLIENT-PC`, from the repository root:

```powershell
$ErrorActionPreference = "Stop"
& ".\src\Hase.Client.Wpf.App\bin\Release\net10.0-windows\Hase.Client.Wpf.App.exe" `
    (Join-Path $env:LocalAppData "HASE\Secured\client-runtime-hosts.json")
```

Press `Connect` on `Example Host (secured)`: the tile turns green and the
host's endpoints appear — every read, write, Command, and Event now
crossing the network under mutual TLS.

## Failure behavior

Everything fails closed. A missing, unenrolled, or wrong certificate, a
server certificate that differs from the client's pin, a malformed or
oversized document, or an unknown JSON field each stop the affected side
with an error — never with a cleartext, wildcard, or unauthenticated
fallback. Typical first-run issues:

- **Client cannot reach the host** — verify `<HOST-IP>`, the firewall rule
  of Step 6, and that both PCs are on the same (non-guest) network.
- **TLS or enrollment rejection** — the three transferred files and the
  host's generated documents must come from the same provisioning run;
  re-run Step 1 into a fresh directory if in doubt (the script refuses to
  overwrite existing targets).
- **Identity mismatch** — `expectedRuntimeHostId` in the client registry
  must equal the host's `runtime-host-identity.json`.

## Where to go next

Example 3 walks this provisioning end to end, and the guided setup wizard
automates Steps 1 through 5. Both are in preparation under ADR-0060. The
[Private-Network Credential Provisioning](Private-Network-Credential-Provisioning.md)
reference describes the underlying validation profile, and the
[Northbound API Reference](API%20reference/HASE-Northbound-API-Reference.md)
documents the secured API itself.
