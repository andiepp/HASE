# ADR-0054 — Increment 54E1 — Physical Deployment Preflight

## Goal and scope

Increment 54E1 establishes fail-closed readiness for the later, separately
authorized ESP32 firmware deployment. It runs only on AEPRAKETE and validates:

- the exact clean `main` repository at commit
  `af513822d0b79e18cc979798ed409b1bfdffd7f3`;
- stopped Desktop Runtime Host and HASE Client processes;
- the approved Arduino CLI executable and hash, CLI 1.3.1, ESP32 core 3.3.10,
  and FQBN `esp32:esp32:esp32doit-devkit-v1`;
- one explicit operator-selected COM port in the Arduino CLI inventory;
- local, present, ignored, and untracked `HaseSecrets.h` custody without
  reading its contents;
- the exact five-file application surface and preserved endpoint capability
  identities; and
- availability of rollback source commit
  `96db1799d410eedc82aea82cc3f5b3efa003242c` and its 122-path historical
  `HaseEndpoint` tree.

USB-to-serial enumeration does not prove the ESP32 board model or authoritative
HASE endpoint identity. The operator selects the connected port, the repository
fixes the FQBN, and later Protocol discovery validates endpoint identity after
deployment.

## Automated validation

The increment adds thirteen tests:

- one all-ready assessment;
- nine independent fail-closed assessment cases;
- one no-recovery result for the non-mutating preflight;
- one PowerShell safety-contract test; and
- one Windows PowerShell parser test.

Expected complete .NET Release result: **5,987 passed, 0 failed**.

The parser test uses the conventional Windows PowerShell executable on Windows.
Returning successfully on a non-Windows test machine is not equivalent parser
evidence for the operational script.

## AEPRAKETE execution

Do not run this block until the source increment has passed focused and complete
automated validation and has been explicitly released for physical preflight.

Keep the Desktop Runtime Host and HASE Client stopped. Determine the connected
ESP32 COM port in Windows Device Manager or Arduino IDE, then close any program
that might own that port. Choose a new evidence directory outside the
repository.

First parse the script without executing it:

```powershell
cd H:\Development

$scriptPath = Join-Path `
    (Get-Location).Path `
    "tools\Arduino\Test-HaseEsp32PhysicalDeploymentPreflight.ps1"

$tokens = $null
$parseErrors = $null

[System.Management.Automation.Language.Parser]::ParseFile(
    $scriptPath,
    [ref]$tokens,
    [ref]$parseErrors) | Out-Null

if (@($parseErrors).Count -ne 0)
{
    $parseErrors | ForEach-Object { Write-Host $_.Message }
    throw "The 54E1 preflight script did not parse."
}
```

Then execute it, replacing only the explicit port value:

```powershell
$esp32Port = "<operator-selected-COM-port>"
$evidenceRoot = "H:\HASE-Packages\HASE-ADR-0054-54E1-Evidence"

& $scriptPath `
    -RepositoryRoot "H:\Development" `
    -Port $esp32Port `
    -EvidenceRoot $evidenceRoot
```

The placeholder in this documentation is not executable text. Replace it with
the actual operator-selected value such as `COM7`; do not paste the angle
brackets into PowerShell.

Expected Boolean outcomes are:

- computer exact;
- repository baseline exact and clean;
- deployment processes stopped;
- Arduino CLI, ESP32 core, and FQBN exact;
- operator port detected;
- local secrets ready but not read;
- application contract exact;
- rollback source ready;
- repository unchanged;
- firmware compiled false;
- firmware uploaded false;
- serial port opened false; and
- physical state changed false.

The retained `preflight.json` withholds the selected port and contains no
credentials, serial number, private address, certificate, or local-secret
content. The script reports its SHA-256 for later correlation.

## Existing physical behavior baseline

The pre-deployment behavior baseline remains the previously accepted physical
ESP32 contract:

- authoritative endpoint `doit-esp32-devkitc-v4-01`;
- temperature, relative-humidity, and air-pressure reads;
- status-LED read/write and toggle behavior on GPIO16;
- live button Event behavior on GPIO17;
- TCP port 5000, mDNS publication, UTC timestamps, and reconnect recovery.

54E1 verifies that the migrated source declares the same identities. It does
not start the installed Runtime Host, query the currently running firmware, or
exercise hardware. A fresh before/after physical comparison belongs to the
separately authorized 54E2/54E3 operations.

## Rollback boundary

No rollback is required after a successful or rejected 54E1 preflight because
it performs no compilation, upload, serial-port open, or physical mutation.
It changes no tracked or untracked repository file. Its only created artifact
is the new outside-repository evidence directory after all readiness checks
have succeeded; that evidence is retained.

If a later firmware deployment requires rollback, the authoritative source is
the detached historical commit
`96db1799d410eedc82aea82cc3f5b3efa003242c`. A separately authorized recovery
procedure will stage that source outside the active repository, copy the local
ignored `HaseSecrets.h` without displaying it, compile it with the approved
toolchain and FQBN, upload it only to the explicitly selected port, and repeat
the physical compatibility checks. 54E1 proves source availability but does
not execute any recovery command.

## Definition of done

54E1 is complete when:

1. focused and complete Release tests pass;
2. the exact source diff is reviewed, committed, and pushed;
3. AEPRAKETE, LABC, and LTAEP are synchronized and clean;
4. the approved preflight runs on AEPRAKETE and all computed readiness results
   match the expected non-mutating outcome; and
5. the retained evidence hash is recorded without exposing protected values.

No firmware deployment is authorized by completion of 54E1.
