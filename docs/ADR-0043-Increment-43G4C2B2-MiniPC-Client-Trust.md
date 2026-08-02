# ADR-0043 — Increment 43G4C2B2 — MiniPC Client Trust

## Decision

The laptop may prepare a second, separate private-network Client configuration
for the MiniPC before the MiniPC Runtime Host is installed. The workflow trusts
only the MiniPC's explicitly transferred public server certificate and reuses
the laptop's existing client certificate and private key by certificate-store
reference.

The laptop client private key is never imported, exported, copied, or replaced.
The existing Desktop Runtime Host Client configuration and multi-host registry
remain byte-for-byte unchanged.

## Preconditions

Run the workflow on the laptop only, after a Release build. Transfer only
`runtime-host-server.cer` from the MiniPC. Supply the MiniPC listener address
and port locally; do not place either value in source control or conversation
output.

## Command contract

```powershell
& .\tools\Deployment\Install-HaseMiniPcClientTrust.ps1 `
  -PublicServerCertificatePath "<transferred-public-CER>" `
  -ListenerAddress "<MiniPC-private-address>" `
  -Port <port> `
  -ExistingClientConfigurationPath "<existing-desktop-host-client-config>" `
  -ClientRegistryPath "<existing-client-runtime-hosts-registry>" `
  -OutputConfigurationPath "<new-minipc-private-network-json>"
```

The workflow refuses a missing source, an invalid or loopback listener, or an
existing target configuration. It imports the public server certificate into
`CurrentUser\TrustedPeople`, stages the MiniPC configuration, validates it with
the strict Client reader, requires the existing client certificate private key,
and verifies the trusted certificate against the transferred public file.

If validation fails, the staged configuration and any certificate newly
imported by this run are removed. A certificate that was already trusted before
the run is never removed.

## Result and exclusions

Successful execution publishes only `minipc-private-network.json` and preserves
the existing Client registry. No MiniPC profile is added because an
authoritative MiniPC Runtime Host identity does not exist until installation.

This increment does not connect to either Runtime Host, start or install the
MiniPC Runtime Host, change firewall state, transfer host configuration or
private keys, or modify the desktop PC or MiniPC.

Console output withholds addresses, ports, paths, certificate metadata,
credential identifiers, and configuration contents.
