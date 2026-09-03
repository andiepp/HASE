param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = "H:\Development",

    [Parameter(Mandatory = $false)]
    [string]$ArduinoCliPath =
        "I:\Arduino\arduino-ide_2.3.7\resources\app\lib\backend\resources\arduino-cli.exe",

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-fA-F]{40}$")]
    [string]$ExpectedCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^COM[1-9][0-9]*$")]
    [string]$Port,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rollbackCommit =
    "96db1799d410eedc82aea82cc3f5b3efa003242c"
$expectedCliHash =
    "7c4f90d6b1f640975a0f0ed3fab8a93f969e0ce0058c99bda69f07228d50cb6b"
$expectedCliVersion = "1.3.1"
$expectedCoreVersion = "3.3.10"
$expectedFqbn = "esp32:esp32:esp32doit-devkit-v1"

$expectedApplicationFiles = @(
    "EndpointApplication.cpp",
    "EndpointApplication.h",
    "EndpointConfiguration.h",
    "EndpointDefinition.cpp",
    "HaseESP32.ino"
) | Sort-Object

$expectedDefinitionTokens = @(
    '"doit-esp32-devkitc-v4-01"',
    '"Environment.Temperature"',
    '"Environment.RelativeHumidity"',
    '"Environment.AirPressure"',
    '"Controller.StatusLedEnabled"',
    '"Controller.ToggleStatusLed"',
    '"Controller.ButtonPressed"'
)

function Invoke-GitLines
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = @(& git -C $RepositoryRoot @Arguments)

    if ($LASTEXITCODE -ne 0)
    {
        $message = "git failed: {0}" -f ($Arguments -join " ")
        throw $message
    }

    return $output
}

function Assert-ExactStringSet
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$Expected,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$Actual,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $sortedExpected = @($Expected | Sort-Object)
    $sortedActual = @($Actual | Sort-Object)

    if ($sortedExpected.Count -ne $sortedActual.Count)
    {
        $message = "The {0} set is invalid." -f $Description
        throw $message
    }

    for ($index = 0; $index -lt $sortedExpected.Count; $index++)
    {
        if ($sortedExpected[$index] -cne $sortedActual[$index])
        {
            $message = "The {0} set is invalid." -f $Description
            throw $message
        }
    }
}

function Invoke-ArduinoCli
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference

    try
    {
        $ErrorActionPreference = "Continue"
        $output = @(
            & $ArduinoCliPath @Arguments 2>&1 |
                ForEach-Object { $_.ToString() }
        )
        $exitCode = $LASTEXITCODE
    }
    finally
    {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0)
    {
        $message = "Arduino CLI inspection failed: {0}" -f ($Arguments -join " ")
        throw $message
    }

    return $output
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$ArduinoCliPath = [System.IO.Path]::GetFullPath($ArduinoCliPath)
$EvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()

if ($env:COMPUTERNAME -cne $expectedComputer)
{
    throw "The ESP32 deployment preflight must run on $expectedComputer."
}

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container))
{
    throw "The repository root does not exist."
}

$repositoryPrefix = $RepositoryRoot.TrimEnd('\') + "\"

if ($EvidenceRoot.StartsWith(
        $repositoryPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The preflight evidence directory must be outside the repository."
}

if (Test-Path -LiteralPath $EvidenceRoot)
{
    throw "The preflight evidence directory already exists."
}

if (-not (Test-Path -LiteralPath $ArduinoCliPath -PathType Leaf))
{
    throw "The approved embedded Arduino CLI is missing."
}

foreach ($processName in @(
    "Hase.DesktopHost.App",
    "Hase.Client.Wpf.App"))
{
    if (@(Get-Process -Name $processName -ErrorAction SilentlyContinue).Count -ne 0)
    {
        $message = "A deployment-bound process is running: {0}" -f $processName
        throw $message
    }
}

& git -C $RepositoryRoot fetch origin main

if ($LASTEXITCODE -ne 0)
{
    throw "git fetch origin main failed."
}

$head = @(Invoke-GitLines -Arguments @("rev-parse", "HEAD"))
$origin = @(Invoke-GitLines -Arguments @("rev-parse", "origin/main"))
$branch = @(Invoke-GitLines -Arguments @("branch", "--show-current"))
$statusBefore = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

if ($head.Count -ne 1 -or $head[0].Trim() -cne $ExpectedCommit)
{
    throw "Repository HEAD is not the explicitly approved baseline."
}

if ($origin.Count -ne 1 -or $origin[0].Trim() -cne $ExpectedCommit)
{
    throw "origin/main is not the explicitly approved baseline."
}

if ($branch.Count -ne 1 -or $branch[0].Trim() -cne "main")
{
    throw "The repository is not on main."
}

if ($statusBefore.Count -ne 0)
{
    throw "The repository is not clean."
}

$actualCliHash =
    (Get-FileHash -LiteralPath $ArduinoCliPath -Algorithm SHA256).
        Hash.ToLowerInvariant()

if ($actualCliHash -cne $expectedCliHash)
{
    throw "The embedded Arduino CLI hash changed."
}

$powerShellDirectory =
    Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0"
$powerShellExecutable =
    Join-Path $powerShellDirectory "powershell.exe"

if (-not (Test-Path -LiteralPath $powerShellExecutable -PathType Leaf))
{
    throw "The conventional Windows PowerShell executable is missing."
}

$env:Path = $powerShellDirectory + ";" + $env:Path

$versionOutput = @(Invoke-ArduinoCli -Arguments @("version"))

if (@($versionOutput | Where-Object {
        $_ -match [regex]::Escape($expectedCliVersion)
    }).Count -eq 0)
{
    throw "The Arduino CLI version does not match the approved version."
}

$coreOutput = @(Invoke-ArduinoCli -Arguments @("core", "list"))
$corePattern = "^esp32:esp32\s+{0}(\s|$)" -f (
    [regex]::Escape($expectedCoreVersion))

if (@($coreOutput | Where-Object { $_ -match $corePattern }).Count -ne 1)
{
    throw "The approved ESP32 core version is not installed exactly once."
}

$boardDefinitions = @(
    Invoke-ArduinoCli -Arguments @("board", "listall")
)

if (@($boardDefinitions | Where-Object {
        $_ -match [regex]::Escape($expectedFqbn)
    }).Count -ne 1)
{
    throw "The approved ESP32 board definition is not available exactly once."
}

$boardJsonLines = @(
    Invoke-ArduinoCli -Arguments @("board", "list", "--format", "json")
)

try
{
    $boardDocument =
        ($boardJsonLines -join [System.Environment]::NewLine) |
            ConvertFrom-Json
    $detectedPorts = @($boardDocument.detected_ports)
    $selectedPorts = @(
        $detectedPorts |
            Where-Object {
                [string]::Equals(
                    [string]$_.port.address,
                    $Port,
                    [System.StringComparison]::OrdinalIgnoreCase)
            }
    )
}
catch
{
    throw "The Arduino CLI board inventory could not be interpreted."
}

if ($selectedPorts.Count -ne 1)
{
    throw "The operator-selected ESP32 port was not detected exactly once."
}

$localSecretsPath =
    Join-Path $RepositoryRoot "HaseESP32\HaseSecrets.h"

if (-not (Test-Path -LiteralPath $localSecretsPath -PathType Leaf))
{
    throw "The local ESP32 Wi-Fi secrets file is missing."
}

& git -C $RepositoryRoot check-ignore --quiet -- "HaseESP32/HaseSecrets.h"

if ($LASTEXITCODE -ne 0)
{
    throw "The local ESP32 Wi-Fi secrets path is not ignored."
}

$trackedSecrets = @(
    Invoke-GitLines -Arguments @("ls-files", "--", "HaseESP32/HaseSecrets.h")
)

if ($trackedSecrets.Count -ne 0)
{
    throw "The local ESP32 Wi-Fi secrets file is unexpectedly tracked."
}

$applicationRoot = Join-Path $RepositoryRoot "HaseESP32"
$actualApplicationFiles = @(
    Get-ChildItem -LiteralPath $applicationRoot -File |
        Where-Object {
            $_.Extension -in @(".ino", ".cpp", ".h") -and
            $_.Name -cne "HaseSecrets.h"
        } |
        ForEach-Object { $_.Name } |
        Sort-Object
)

Assert-ExactStringSet `
    -Expected $expectedApplicationFiles `
    -Actual $actualApplicationFiles `
    -Description "active application source file"

$definitionPath = Join-Path $applicationRoot "EndpointDefinition.cpp"
$definitionText = Get-Content -LiteralPath $definitionPath -Raw

foreach ($token in $expectedDefinitionTokens)
{
    if ([regex]::Matches(
            $definitionText,
            [regex]::Escape($token)).Count -ne 1)
    {
        throw "The active endpoint capability contract changed."
    }
}

& git -C $RepositoryRoot cat-file -e ($rollbackCommit + "^{commit}")

if ($LASTEXITCODE -ne 0)
{
    throw "The approved rollback commit is not available locally."
}

$rollbackPaths = @(
    Invoke-GitLines `
        -Arguments @(
            "ls-tree",
            "-r",
            "--name-only",
            $rollbackCommit,
            "--",
            "HaseESP32")
)

if ($rollbackPaths.Count -ne 122 -or
    $rollbackPaths -notcontains "HaseESP32/HaseESP32.ino")
{
    throw "The approved rollback source tree changed."
}

$statusAfter = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

Assert-ExactStringSet `
    -Expected $statusBefore `
    -Actual $statusAfter `
    -Description "repository status"

[System.IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$evidencePath = Join-Path $EvidenceRoot "preflight.json"
$evidence = [ordered]@{
    formatVersion = 1
    increment = "54E1"
    computer = $expectedComputer
    repositoryCommit = $ExpectedCommit
    rollbackCommit = $rollbackCommit
    arduinoCliVersion = $expectedCliVersion
    esp32CoreVersion = $expectedCoreVersion
    fqbn = $expectedFqbn
    selectedPort = "Withheld"
    localSecretsPresent = $true
    localSecretsRead = $false
    runtimeHostStopped = $true
    clientStopped = $true
    repositoryUnchanged = $true
    firmwareCompiled = $false
    firmwareUploaded = $false
    serialPortOpened = $false
    physicalStateChanged = $false
}

[System.IO.File]::WriteAllText(
    $evidencePath,
    ($evidence | ConvertTo-Json -Depth 4),
    (New-Object System.Text.UTF8Encoding($false)))

$evidenceHash =
    (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).
        Hash.ToLowerInvariant()

Write-Host ""
Write-Host "ADR-0054 Increment 54E1 physical deployment preflight"
Write-Host ""
Write-Host "Computer exact             :" $true
Write-Host "Repository baseline exact  :" $true
Write-Host "Repository clean           :" $true
Write-Host "Deployment processes stopped:" $true
Write-Host "Arduino CLI version exact  :" $true
Write-Host "ESP32 core version exact   :" $true
Write-Host "FQBN exact                 :" $true
Write-Host "Operator port detected     :" $true
Write-Host "Local secrets ready        :" $true
Write-Host "Local secrets read         :" $false
Write-Host "Application contract exact :" $true
Write-Host "Rollback source ready      :" $true
Write-Host "Repository unchanged       :" $true
Write-Host "Firmware compiled          :" $false
Write-Host "Firmware uploaded          :" $false
Write-Host "Serial port opened         :" $false
Write-Host "Physical state changed     :" $false
Write-Host "Evidence SHA-256           :" $evidenceHash
Write-Host ""
Write-Host "The evidence directory is retained outside the repository."
Write-Host "No compilation, upload, serial-port open, or physical mutation was performed."
