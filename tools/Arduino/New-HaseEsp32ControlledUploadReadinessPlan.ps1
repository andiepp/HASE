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
    [ValidatePattern("^[0-9a-fA-F]{40}$")]
    [string]$ExpectedBundleRepositoryCommit,

    [Parameter(Mandatory = $true)]
    [string]$BundleRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-fA-F]{64}$")]
    [string]$ExpectedBundleManifestSha256,

    [Parameter(Mandatory = $true)]
    [string]$PreparationEvidencePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-fA-F]{64}$")]
    [string]$ExpectedPreparationEvidenceSha256,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^COM[1-9][0-9]*$")]
    [string]$Port,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4}$")]
    [string]$VendorProduct,

    [Parameter(Mandatory = $true)]
    [string]$PlanRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expectedComputer = "AEPRAKETE"
$expectedCliHash =
    "7c4f90d6b1f640975a0f0ed3fab8a93f969e0ce0058c99bda69f07228d50cb6b"
$expectedCliVersion = "1.3.1"
$expectedCoreVersion = "3.3.10"
$fqbn = "esp32:esp32:esp32doit-devkit-v1"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Invoke-GitLines
{
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = @(& git -C $RepositoryRoot @Arguments)

    if ($LASTEXITCODE -ne 0)
    {
        throw ("git failed: {0}" -f ($Arguments -join " "))
    }

    return $output
}

function Invoke-ArduinoCli
{
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

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

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Get-ActualArtifactSignatures
{
    param([Parameter(Mandatory = $true)][string]$Root)

    $fullRoot = [System.IO.Path]::GetFullPath($Root)
    $prefix = $fullRoot.TrimEnd('\') + "\"

    return @(
        Get-ChildItem -LiteralPath $fullRoot -File -Recurse |
            ForEach-Object {
                $name = $_.FullName.Substring($prefix.Length).Replace('\', '/')
                $hash = (Get-FileHash `
                    -LiteralPath $_.FullName `
                    -Algorithm SHA256).Hash.ToLowerInvariant()
                "{0}|{1}|{2}" -f $name, $_.Length, $hash
            } |
            Sort-Object
    )
}

function Get-ManifestArtifactSignatures
{
    param([Parameter(Mandatory = $true)][object[]]$Artifacts)

    return @(
        $Artifacts |
            ForEach-Object {
                "{0}|{1}|{2}" -f $_.name, $_.length, $_.sha256
            } |
            Sort-Object
    )
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

        [Parameter(Mandatory = $true)][string]$Description
    )

    if (@(Compare-Object `
            -ReferenceObject $Expected `
            -DifferenceObject $Actual `
            -CaseSensitive).Count -ne 0)
    {
        throw ("The {0} set is invalid." -f $Description)
    }
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$ArduinoCliPath = [System.IO.Path]::GetFullPath($ArduinoCliPath)
$BundleRoot = [System.IO.Path]::GetFullPath($BundleRoot)
$PreparationEvidencePath =
    [System.IO.Path]::GetFullPath($PreparationEvidencePath)
$PlanRoot = [System.IO.Path]::GetFullPath($PlanRoot)
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()
$ExpectedBundleRepositoryCommit =
    $ExpectedBundleRepositoryCommit.ToLowerInvariant()
$ExpectedBundleManifestSha256 =
    $ExpectedBundleManifestSha256.ToLowerInvariant()
$ExpectedPreparationEvidenceSha256 =
    $ExpectedPreparationEvidenceSha256.ToLowerInvariant()
$VendorProduct = $VendorProduct.ToUpperInvariant()

if ($env:COMPUTERNAME -cne $expectedComputer)
{
    throw "The controlled-upload readiness plan must be created on AEPRAKETE."
}

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container))
{
    throw "The repository root does not exist."
}

$repositoryPrefix = $RepositoryRoot.TrimEnd('\') + "\"
$localCustodyRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "HASE\Esp32DeploymentBundles"))
$localCustodyPrefix = $localCustodyRoot.TrimEnd('\') + "\"

if (-not $BundleRoot.StartsWith(
        $localCustodyPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The sensitive bundle is outside current-user local HASE custody."
}

if ($PlanRoot.StartsWith(
        $repositoryPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The readiness-plan root must be outside the repository."
}

if (Test-Path -LiteralPath $PlanRoot)
{
    throw "The readiness-plan root already exists."
}

foreach ($processName in @(
    "Hase.DesktopHost.App",
    "Hase.Client.Wpf.App",
    "Arduino IDE"))
{
    if (@(Get-Process -Name $processName -ErrorAction SilentlyContinue).Count -ne 0)
    {
        throw ("A controlled-upload process is running: {0}" -f $processName)
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

if ($head.Count -ne 1 -or $head[0].Trim() -cne $ExpectedCommit -or
    $origin.Count -ne 1 -or $origin[0].Trim() -cne $ExpectedCommit)
{
    throw "The repository baseline is not explicitly approved."
}

if ($branch.Count -ne 1 -or $branch[0].Trim() -cne "main" -or
    $statusBefore.Count -ne 0)
{
    throw "The repository is not clean main."
}

if (-not (Test-Path -LiteralPath $ArduinoCliPath -PathType Leaf))
{
    throw "The approved embedded Arduino CLI is missing."
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
$env:Path = $powerShellDirectory + ";" + $env:Path

$versionResult = Invoke-ArduinoCli -Arguments @("version")
$coreResult = Invoke-ArduinoCli -Arguments @("core", "list")
$corePattern = "^esp32:esp32\s+{0}(\s|$)" -f (
    [regex]::Escape($expectedCoreVersion))

if ($versionResult.ExitCode -ne 0 -or
    @($versionResult.Output | Where-Object {
        $_ -match [regex]::Escape($expectedCliVersion)
    }).Count -eq 0 -or
    $coreResult.ExitCode -ne 0 -or
    @($coreResult.Output | Where-Object { $_ -match $corePattern }).Count -ne 1)
{
    throw "The approved Arduino toolchain is not ready."
}

$manifestPath = Join-Path $BundleRoot "bundle-manifest.json"
$currentRoot = Join-Path $BundleRoot "Current"
$rollbackRoot = Join-Path $BundleRoot "Rollback"

foreach ($requiredPath in @(
    $manifestPath,
    $PreparationEvidencePath))
{
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf))
    {
        throw "A required 54E2A custody file is missing."
    }
}

$actualManifestHash =
    (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).
        Hash.ToLowerInvariant()
$actualPreparationEvidenceHash =
    (Get-FileHash -LiteralPath $PreparationEvidencePath -Algorithm SHA256).
        Hash.ToLowerInvariant()

if ($actualManifestHash -cne $ExpectedBundleManifestSha256 -or
    $actualPreparationEvidenceHash -cne $ExpectedPreparationEvidenceSha256)
{
    throw "A 54E2A custody hash is incorrect."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$preparationEvidence =
    Get-Content -LiteralPath $PreparationEvidencePath -Raw | ConvertFrom-Json

if ($manifest.formatVersion -ne 1 -or
    $manifest.increment -cne "54E2A" -or
    $manifest.repositoryCommit -cne $ExpectedBundleRepositoryCommit -or
    $manifest.fqbn -cne $fqbn -or
    $preparationEvidence.repositoryCommit -cne
        $ExpectedBundleRepositoryCommit -or
    $preparationEvidence.bundleManifestSha256 -cne
        $ExpectedBundleManifestSha256 -or
    $preparationEvidence.firmwareCompiled -ne $true -or
    $preparationEvidence.firmwareUploaded -ne $false)
{
    throw "The 54E2A custody semantics are invalid."
}

$actualCurrent = @(Get-ActualArtifactSignatures -Root $currentRoot)
$manifestCurrent = @(
    Get-ManifestArtifactSignatures -Artifacts @($manifest.currentArtifacts))
$actualRollback = @(Get-ActualArtifactSignatures -Root $rollbackRoot)
$manifestRollback = @(
    Get-ManifestArtifactSignatures -Artifacts @($manifest.rollbackArtifacts))

Assert-ExactStringSet `
    -Expected $manifestCurrent `
    -Actual $actualCurrent `
    -Description "current firmware artifact"
Assert-ExactStringSet `
    -Expected $manifestRollback `
    -Actual $actualRollback `
    -Description "rollback firmware artifact"

$portPattern = "\({0}\)$" -f [regex]::Escape($Port)
$matchingDevices = @(
    Get-PnpDevice -Class Ports -PresentOnly -ErrorAction Stop |
        Where-Object {
            $_.FriendlyName -match $portPattern -and
            $_.InstanceId -match [regex]::Escape($VendorProduct) -and
            $_.Status -ceq "OK"
        }
)

if ($matchingDevices.Count -ne 1)
{
    throw "The operator-selected ESP32 device identity was not detected exactly once."
}

$statusAfter = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)
Assert-ExactStringSet `
    -Expected $statusBefore `
    -Actual $statusAfter `
    -Description "repository status"

[System.IO.Directory]::CreateDirectory($PlanRoot) | Out-Null
$planPath = Join-Path $PlanRoot "controlled-upload-readiness-plan.json"
$plan = [ordered]@{
    formatVersion = 1
    increment = "54E2B1"
    computer = $expectedComputer
    repositoryCommit = $ExpectedCommit
    bundleRepositoryCommit = $ExpectedBundleRepositoryCommit
    arduinoCliSha256 = $expectedCliHash
    arduinoCliVersion = $expectedCliVersion
    esp32CoreVersion = $expectedCoreVersion
    fqbn = $fqbn
    bundleManifestSha256 = $ExpectedBundleManifestSha256
    preparationEvidenceSha256 = $ExpectedPreparationEvidenceSha256
    selectedPort = $Port
    vendorProduct = $VendorProduct
    currentArtifacts = @($manifest.currentArtifacts)
    rollbackArtifacts = @($manifest.rollbackArtifacts)
    repositoryUnchanged = $true
    firmwareUploaded = $false
    serialPortOpened = $false
    physicalStateChanged = $false
}
[System.IO.File]::WriteAllText(
    $planPath,
    ($plan | ConvertTo-Json -Depth 8),
    $utf8NoBom)

$planHash =
    (Get-FileHash -LiteralPath $planPath -Algorithm SHA256).
        Hash.ToLowerInvariant()

Write-Host ""
Write-Host "ADR-0054 Increment 54E2B1 controlled-upload readiness"
Write-Host "Computer exact              :" $true
Write-Host "Repository baseline exact   :" $true
Write-Host "Repository clean            :" $true
Write-Host "Controlled processes stopped:" $true
Write-Host "Toolchain exact             :" $true
Write-Host "Bundle manifest exact       :" $true
Write-Host "Preparation evidence exact  :" $true
Write-Host "Current artifacts exact     :" $true
Write-Host "Rollback artifacts exact    :" $true
Write-Host "Device identity exact       :" $true
Write-Host "Repository unchanged        :" $true
Write-Host "Firmware uploaded           :" $false
Write-Host "Serial port opened          :" $false
Write-Host "Physical state changed      :" $false
Write-Host "Readiness plan SHA-256      :" $planHash
Write-Host ""
Write-Host "No upload, serial-port open, reset, retry, rollback, or physical mutation was performed."
