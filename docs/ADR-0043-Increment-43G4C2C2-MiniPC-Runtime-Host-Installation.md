# ADR-0043 — Increment 43G4C2C2 — MiniPC Runtime Host Installation

## Discussion

This increment installs a distinct Arduino-only Runtime Host on the MiniPC. It deliberately reuses the security material provisioned and validated in 43G4C2B1, while giving the installation a new locally-custodied authoritative Runtime Host identity.

The MiniPC endpoint composition is intentionally exact: one Compact Serial endpoint, expected endpoint ID `arduino-uno-01`, USB vendor/product IDs `0x2341`/`0x0001`, 115200 baud, and a three-second verification timeout. No ESP32, native-network endpoint, or simulated endpoint is installed.

The installation remains separate from Client onboarding. This increment does not start the Runtime Host, change the laptop Client registry, open a firewall port, or perform a network connection test.

## Implement now

The new `Install-HaseMiniPcRuntimeHost.ps1` orchestrator:

1. refuses an existing Runtime Host directory or desktop shortcut;
2. locates the already-provisioned MiniPC private-network configuration and enrollment;
3. records security-document hashes and the matching CurrentUser certificate count;
4. runs the strict second-PC preflight;
5. requires exactly one attached Arduino with the exact USB identity;
6. runs authoritative C-020 validation against the resolved serial port;
7. invokes the guided `CompactSerialOnly` installer with the exact MiniPC contract;
8. creates a new installation-local Runtime Host identity;
9. audits the completed installation;
10. proves the source security documents and certificate-store population were preserved.

Command output reports readiness only. Serial-port, address, certificate, enrollment, key, and Runtime Host identity values are withheld.

If an error occurs after installation begins, only the newly attempted `%LOCALAPPDATA%\HASE\RuntimeHost` directory and `HASE Runtime Host.lnk` shortcut are removed. The provisioned source security directory and CurrentUser certificate store are not rolled back or modified.

## Automated validation

On the desktop, apply the overlay, build the entire solution in Release, and run all tests. Expected result: **4,381 passed, 0 failed**.

The eight new tests cover:

- the all-ready installation assessment;
- fail-closed behavior for each of its six readiness inputs;
- the exact MiniPC Compact Serial physical contract.

## MiniPC validation

Perform these steps on the MiniPC only, after synchronizing the source verified on the desktop.

1. Confirm the Runtime Host is not running.
2. Confirm the Arduino Uno containing descriptor-v2 firmware is connected.
3. Open PowerShell in `H:\Development`.
4. If required for this PowerShell process only, run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

5. Build the solution in Release so the preflight, C-020, and audit executables are current.
6. Run:

```powershell
& .\tools\Deployment\Install-HaseMiniPcRuntimeHost.ps1
```

Expected safe summary:

```text
HASE Arduino-only MiniPC Runtime Host installation succeeded.
Security preflight        : Ready
Authoritative Arduino     : Ready
Endpoint composition      : CompactSerialOnly
Runtime Host identity     : Created
Installation audit        : Ready
Provisioned security      : Preserved
Sensitive deployment values: Withheld
```

7. Confirm the Runtime Host was not automatically started.
8. Confirm `%LOCALAPPDATA%\HASE\RuntimeHost` and the desktop `HASE Runtime Host` shortcut now exist.
9. Confirm the provisioned source security directory still exists.

Do not paste private addresses, certificate identifiers, enrollment contents, serial-port details, or generated Runtime Host identities into chat or commit them to source control.

## Backlog

- transfer the new MiniPC Runtime Host identity to the laptop through the controlled handoff workflow;
- add the MiniPC profile to the laptop Client registry;
- validate firewall/listener ownership and the private-network connection;
- exercise simultaneous multi-host Client behavior.

## Stop point

Stop after the installer reports success and the filesystem checks pass. Do not launch the newly installed Runtime Host or edit the laptop Client registry in this increment.
