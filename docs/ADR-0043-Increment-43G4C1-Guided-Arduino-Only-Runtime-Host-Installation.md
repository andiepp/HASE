# ADR-0043 — Increment 43G4C1 — Guided Arduino-Only Runtime Host Installation

## Decision

The guided Desktop Runtime Host installer supports two explicit endpoint-
composition modes:

- `DefaultPhysical` preserves the existing ESP32 plus Arduino Uno workflow and
  remains the default;
- `CompactSerialOnly` creates exactly one explicitly configured compact-serial
  endpoint and does not request or write an ESP32 host/address.

The compact endpoint owns an expected endpoint ID, exact `0xNNNN` USB vendor
and product IDs, positive baud rate, and a verification timeout from 1 through
60,000 milliseconds.

## Arduino-only command

Run only on a Windows machine without an existing guided Runtime Host
installation, using an already provisioned private-network host configuration:

```powershell
& .\tools\Deployment\Install-HaseDesktopRuntimeHost.ps1 `
  -EndpointCompositionMode CompactSerialOnly `
  -CompactExpectedEndpointId "arduino-uno-01" `
  -CompactVendorId "0x2341" `
  -CompactProductId "0x0043" `
  -CompactBaudRate 115200 `
  -CompactVerificationTimeoutMilliseconds 3000
```

The installer still prompts for the fully qualified existing private-network
host configuration. It publishes the application, creates external profiles,
and creates the desktop shortcut under the established custody rules.

## Identity and security boundaries

The new installation uses its own `%LOCALAPPDATA%\HASE\RuntimeHost\Identity`
path. The installer does not copy or pre-create another machine's identity
file; first authoritative startup creates the installation-local identity.

The private-network configuration, certificate provisioning, and client
enrollment remain separate prerequisites. Output contains no private address,
certificate thumbprint, credential, or enrollment content.

The same endpoint ID may occur on different Runtime Hosts. Client operations
remain qualified by Runtime Host ID, endpoint ID, and attachment generation.

## Deferred physical work

43G4C1 changes and tests plan generation only. Provisioning and installing the
second Windows 11 PC, validating its second Arduino Uno, and creating its
Runtime Host handoff require later approved increments.
