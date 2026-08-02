# ADR-0043 — Increment 43G4C2C3 — MiniPC Handoff and Laptop Profile

## Discussion

This increment turns the installed MiniPC Runtime Host identity into a controlled, non-secret onboarding handoff and consumes that handoff on the laptop as a second enabled Client-local profile. It performs configuration only: neither Runtime Host nor the Client is started.

The existing strict handoff reader, immutable registry contracts, strict registry reader, atomic replacement, and backup retention remain authoritative. The established disabled handoff-import operation is unchanged. A new explicitly named enabled import is used only after the MiniPC laptop trust configuration already exists.

## Implement now

### Automated validation on the desktop

Apply the overlay, build the complete solution in Release, and run all tests. Expected result: **4,390 passed, 0 failed**.

The nine new tests cover enabled handoff import, duplicate enabled authoritative identity rejection, the all-ready onboarding assessment, and fail-closed behavior for each of its six readiness inputs.

### MiniPC handoff creation

Keep the installed MiniPC Runtime Host stopped. Build Release and run from `H:\Development`:

```powershell
$handoffPath = Join-Path `
    $env:LOCALAPPDATA `
    "HASE\SecondRuntimeHostProvisioning\minipc-runtime-host-handoff.json"

& .\tools\Deployment\New-HaseMiniPcRuntimeHostOnboardingHandoff.ps1 `
    -DestinationPath $handoffPath
```

The destination must not already exist. Expected safe results:

```text
Installation audit          : Ready
Handoff format              : Ready
Runtime Host identity       : Withheld
Installed Runtime Host state: Preserved
Sensitive deployment values : Withheld
```

Transfer that single handoff file to the laptop through the approved local transfer method. Do not place it in source control. Although it contains no credentials or network details, do not paste its installation-specific identity into chat.

### Laptop profile installation

Keep the HASE Client closed. Copy the handoff to a known laptop-local path. Confirm that the existing `client-runtime-hosts.json` and previously installed `minipc-private-network.json` remain under the Client configuration directory. Then run from `H:\Development`:

```powershell
$handoffPath = "<fully-qualified-laptop-handoff-path>"

& .\tools\Deployment\Install-HaseMiniPcClientProfile.ps1 `
    -HandoffPath $handoffPath
```

The wrapper requires the current registry to contain exactly one enabled profile. It invokes the strict enabled handoff import, retains the prior registry backup, then verifies exactly two enabled profiles, distinct authoritative identities, unchanged Desktop profile content, unchanged handoff, and unchanged private Client configurations.

Expected safe results:

```text
Handoff validation             : Ready
Desktop Runtime Host profile   : Preserved
MiniPC Runtime Host profile    : Enabled
Authoritative host identities  : Distinct
Private Client configurations  : Preserved
Previous Client registry backup: Retained
Sensitive deployment values    : Withheld
```

If a wrapper postcondition fails after replacement, the newly retained backup is copied back over the active registry. Existing configuration and handoff files are never deleted or overwritten.

## Backlog

- start the MiniPC Runtime Host and validate listener ownership;
- connect the laptop Client to the MiniPC profile;
- start the Desktop and MiniPC Runtime Hosts simultaneously;
- validate independent selection, connection, diagnostics, property, command, and event behavior.

## Stop point

Stop after the laptop profile installation reports success. Do not start the Client or either Runtime Host and do not perform a network connection in this increment.
