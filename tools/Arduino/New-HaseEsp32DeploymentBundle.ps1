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
    [string]$BundleRoot,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rollbackCommit = "96db1799d410eedc82aea82cc3f5b3efa003242c"
$expectedCliHash =
    "7c4f90d6b1f640975a0f0ed3fab8a93f969e0ce0058c99bda69f07228d50cb6b"
$expectedCliVersion = "1.3.1"
$expectedCoreVersion = "3.3.10"
$fqbn = "esp32:esp32:esp32doit-devkit-v1"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$expectedCurrentApplicationFiles = @(
    "EndpointApplication.cpp",
    "EndpointApplication.h",
    "EndpointConfiguration.h",
    "EndpointDefinition.cpp",
    "HaseESP32.ino"
) | Sort-Object

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

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Get-ArtifactManifest
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    $directoryPrefix = $OutputDirectory.TrimEnd('\') + "\"
    $files = @(
        Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse |
            Sort-Object FullName
    )

    if ($files.Count -eq 0)
    {
        throw "A firmware compilation produced no artifacts."
    }

    return @(
        $files |
            ForEach-Object {
                [pscustomobject]@{
                    name = $_.FullName.Substring(
                        $directoryPrefix.Length).Replace('\', '/')
                    length = $_.Length
                    sha256 = (Get-FileHash `
                        -LiteralPath $_.FullName `
                        -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
}

function Assert-DeployableArtifacts
{
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Artifacts,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $binaryArtifacts = @(
        $Artifacts |
            Where-Object { $_.name.EndsWith(".bin") }
    )
    $mainBinary = @(
        $Artifacts |
            Where-Object { $_.name -ceq "HaseESP32.ino.bin" }
    )

    if ($binaryArtifacts.Count -lt 3 -or $mainBinary.Count -ne 1)
    {
        $message = "The {0} artifact set is not deployable." -f $Description
        throw $message
    }
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$ArduinoCliPath = [System.IO.Path]::GetFullPath($ArduinoCliPath)
$BundleRoot = [System.IO.Path]::GetFullPath($BundleRoot)
$EvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()
$workingRoot = $null

if ($env:COMPUTERNAME -cne $expectedComputer)
{
    throw "The ESP32 deployment bundle must be created on $expectedComputer."
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
    throw "The sensitive bundle root must be under the current user's local HASE custody."
}

if ($EvidenceRoot.StartsWith(
        $repositoryPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The evidence root must be outside the repository."
}

if ($EvidenceRoot.StartsWith(
        ($BundleRoot.TrimEnd('\') + "\"),
        [System.StringComparison]::OrdinalIgnoreCase) -or
    $BundleRoot.StartsWith(
        ($EvidenceRoot.TrimEnd('\') + "\"),
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The sensitive bundle and evidence roots must not overlap."
}

if (Test-Path -LiteralPath $BundleRoot)
{
    throw "The sensitive bundle root already exists."
}

if (Test-Path -LiteralPath $EvidenceRoot)
{
    throw "The evidence root already exists."
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
$powerShellExecutable = Join-Path $powerShellDirectory "powershell.exe"

if (-not (Test-Path -LiteralPath $powerShellExecutable -PathType Leaf))
{
    throw "The conventional Windows PowerShell executable is missing."
}

$env:Path = $powerShellDirectory + ";" + $env:Path

$versionResult = Invoke-ArduinoCli -Arguments @("version")

if ($versionResult.ExitCode -ne 0 -or
    @($versionResult.Output | Where-Object {
        $_ -match [regex]::Escape($expectedCliVersion)
    }).Count -eq 0)
{
    throw "Arduino CLI version validation failed."
}

$coreResult = Invoke-ArduinoCli -Arguments @("core", "list")
$corePattern = "^esp32:esp32\s+{0}(\s|$)" -f (
    [regex]::Escape($expectedCoreVersion))

if ($coreResult.ExitCode -ne 0 -or
    @($coreResult.Output | Where-Object { $_ -match $corePattern }).Count -ne 1)
{
    throw "ESP32 core version validation failed."
}

$localSecretsPath = Join-Path $RepositoryRoot "HaseESP32\HaseSecrets.h"

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

& git -C $RepositoryRoot cat-file -e ($rollbackCommit + "^{commit}")

if ($LASTEXITCODE -ne 0)
{
    throw "The approved rollback commit is not available locally."
}

$rollbackPaths = @(
    Invoke-GitLines `
        -Arguments @(
            "ls-tree", "-r", "--name-only",
            $rollbackCommit, "--", "HaseESP32")
)

if ($rollbackPaths.Count -ne 122 -or
    $rollbackPaths -notcontains "HaseESP32/HaseESP32.ino")
{
    throw "The approved rollback source tree changed."
}

try
{
    $workingRoot = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ("HASE-54E2A-" + [guid]::NewGuid().ToString("N"))
    $currentBuildRoot = Join-Path $workingRoot "CurrentBuild"
    $rollbackBuildRoot = Join-Path $workingRoot "RollbackBuild"
    $currentSourceRoot = Join-Path $workingRoot "CurrentSource"
    $currentSketchRoot = Join-Path $currentSourceRoot "HaseESP32"
    $rollbackArchive = Join-Path $workingRoot "RollbackSource.zip"
    $rollbackSourceRoot = Join-Path $workingRoot "RollbackSource"
    $rollbackSketchRoot = Join-Path $rollbackSourceRoot "HaseESP32"

    foreach ($directory in @(
        $workingRoot,
        $currentBuildRoot,
        $rollbackBuildRoot,
        $currentSketchRoot,
        $rollbackSourceRoot,
        $BundleRoot,
        $EvidenceRoot))
    {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $currentOutputRoot = Join-Path $BundleRoot "Current"
    $rollbackOutputRoot = Join-Path $BundleRoot "Rollback"
    [System.IO.Directory]::CreateDirectory($currentOutputRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($rollbackOutputRoot) | Out-Null

    # git archive materializes only tracked rollback source into temporary custody.
    & git -C $RepositoryRoot archive `
        --format=zip `
        --output=$rollbackArchive `
        $rollbackCommit `
        HaseESP32

    if ($LASTEXITCODE -ne 0)
    {
        throw "The approved rollback source archive could not be created."
    }

    Expand-Archive `
        -LiteralPath $rollbackArchive `
        -DestinationPath $rollbackSourceRoot

    [System.IO.File]::Copy(
        $localSecretsPath,
        (Join-Path $rollbackSketchRoot "HaseSecrets.h"),
        $false)

    $repositorySketchRoot = Join-Path $RepositoryRoot "HaseESP32"
    $actualCurrentApplicationFiles = @(
        Get-ChildItem -LiteralPath $repositorySketchRoot -File |
            Where-Object {
                $_.Extension -in @(".ino", ".cpp", ".h") -and
                $_.Name -cne "HaseSecrets.h"
            } |
            ForEach-Object { $_.Name } |
            Sort-Object
    )

    if (@(Compare-Object `
            -ReferenceObject $expectedCurrentApplicationFiles `
            -DifferenceObject $actualCurrentApplicationFiles `
            -CaseSensitive).Count -ne 0)
    {
        throw "The current application source set changed."
    }

    foreach ($applicationFile in $expectedCurrentApplicationFiles)
    {
        [System.IO.File]::Copy(
            (Join-Path $repositorySketchRoot $applicationFile),
            (Join-Path $currentSketchRoot $applicationFile),
            $false)
    }

    [System.IO.File]::Copy(
        $localSecretsPath,
        (Join-Path $currentSketchRoot "HaseSecrets.h"),
        $false)

    $repositoryLibrariesRoot = Join-Path $RepositoryRoot "libraries"
    $currentVendorLibrariesRoot = Join-Path $repositorySketchRoot "Libraries"
    $rollbackVendorLibrariesRoot = Join-Path $rollbackSketchRoot "Libraries"

    $currentArguments = @(
        "compile",
        "--fqbn", $fqbn,
        "--libraries", $repositoryLibrariesRoot,
        "--libraries", $currentVendorLibrariesRoot,
        "--build-path", $currentBuildRoot,
        "--output-dir", $currentOutputRoot,
        "--clean",
        $currentSketchRoot
    )
    $currentResult = Invoke-ArduinoCli -Arguments $currentArguments

    if ($currentResult.ExitCode -ne 0)
    {
        throw "The current library-based firmware compilation failed."
    }

    $rollbackArguments = @(
        "compile",
        "--fqbn", $fqbn,
        "--libraries", $rollbackVendorLibrariesRoot,
        "--build-path", $rollbackBuildRoot,
        "--output-dir", $rollbackOutputRoot,
        "--clean",
        $rollbackSketchRoot
    )
    $rollbackResult = Invoke-ArduinoCli -Arguments $rollbackArguments

    if ($rollbackResult.ExitCode -ne 0)
    {
        throw "The approved rollback firmware compilation failed."
    }

    $currentWarnings = @(
        $currentResult.Output |
            Where-Object { $_ -match "\bwarning:|\bWarnung:" }
    )
    $rollbackWarnings = @(
        $rollbackResult.Output |
            Where-Object { $_ -match "\bwarning:|\bWarnung:" }
    )

    if ($currentWarnings.Count -ne 0 -or $rollbackWarnings.Count -ne 0)
    {
        throw "A deployment firmware compilation produced warnings."
    }

    $currentArtifacts = @(
        Get-ArtifactManifest -OutputDirectory $currentOutputRoot)
    $rollbackArtifacts = @(
        Get-ArtifactManifest -OutputDirectory $rollbackOutputRoot)

    Assert-DeployableArtifacts `
        -Artifacts $currentArtifacts `
        -Description "current firmware"
    Assert-DeployableArtifacts `
        -Artifacts $rollbackArtifacts `
        -Description "rollback firmware"

    $sensitiveFiles = @(
        Get-ChildItem -LiteralPath $BundleRoot -File -Recurse |
            Where-Object { $_.Name -ceq "HaseSecrets.h" }
    )

    if ($sensitiveFiles.Count -ne 0)
    {
        throw "Local secret source was copied into retained bundle custody."
    }

    $bundleManifestPath = Join-Path $BundleRoot "bundle-manifest.json"
    $bundleManifest = [ordered]@{
        formatVersion = 1
        increment = "54E2A"
        repositoryCommit = $ExpectedCommit
        rollbackCommit = $rollbackCommit
        fqbn = $fqbn
        currentArtifacts = $currentArtifacts
        rollbackArtifacts = $rollbackArtifacts
    }
    [System.IO.File]::WriteAllText(
        $bundleManifestPath,
        ($bundleManifest | ConvertTo-Json -Depth 8),
        $utf8NoBom)

    $bundleManifestHash =
        (Get-FileHash -LiteralPath $bundleManifestPath -Algorithm SHA256).
            Hash.ToLowerInvariant()

    $statusAfter = @(
        Invoke-GitLines `
            -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
    )

    $repositoryUnchanged = @(
        Compare-Object `
            -ReferenceObject $statusBefore `
            -DifferenceObject $statusAfter `
            -CaseSensitive
    ).Count -eq 0

    if (-not $repositoryUnchanged)
    {
        throw "The repository changed during deployment-bundle preparation."
    }

    $secretsCopiedToEvidence = @(
        Get-ChildItem -LiteralPath $EvidenceRoot -File -Recurse |
            Where-Object { $_.Name -ceq "HaseSecrets.h" }
    ).Count -ne 0

    if ($secretsCopiedToEvidence)
    {
        throw "Local secret source was copied into retained evidence."
    }

    $evidencePath = Join-Path $EvidenceRoot "deployment-bundle-evidence.json"
    $evidence = [ordered]@{
        formatVersion = 1
        increment = "54E2A"
        computer = $expectedComputer
        repositoryCommit = $ExpectedCommit
        rollbackCommit = $rollbackCommit
        arduinoCliVersion = $expectedCliVersion
        esp32CoreVersion = $expectedCoreVersion
        fqbn = $fqbn
        bundleManifestSha256 = $bundleManifestHash
        currentArtifactCount = $currentArtifacts.Count
        rollbackArtifactCount = $rollbackArtifacts.Count
        currentWarningCount = $currentWarnings.Count
        rollbackWarningCount = $rollbackWarnings.Count
        localSecretsPresent = $true
        secretsCopiedToEvidence = $secretsCopiedToEvidence
        repositoryUnchanged = $repositoryUnchanged
        firmwareCompiled = $true
        firmwareUploaded = $false
        serialPortOpened = $false
        physicalStateChanged = $false
    }
    [System.IO.File]::WriteAllText(
        $evidencePath,
        ($evidence | ConvertTo-Json -Depth 6),
        $utf8NoBom)

    $evidenceHash =
        (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).
            Hash.ToLowerInvariant()

    $evidenceSecretFiles = @(
        Get-ChildItem -LiteralPath $EvidenceRoot -File -Recurse |
            Where-Object { $_.Name -ceq "HaseSecrets.h" }
    )

    if ($evidenceSecretFiles.Count -ne 0)
    {
        throw "Local secret source was copied into retained evidence."
    }

    Write-Host ""
    Write-Host "ADR-0054 Increment 54E2A deployment bundle preparation"
    Write-Host ""
    Write-Host "Computer exact              :" $true
    Write-Host "Repository baseline exact   :" $true
    Write-Host "Repository clean            :" $true
    Write-Host "Deployment processes stopped:" $true
    Write-Host "Arduino CLI version exact   :" $true
    Write-Host "ESP32 core version exact    :" $true
    Write-Host "FQBN exact                  :" $true
    Write-Host "Local secrets ready         :" $true
    Write-Host "Current firmware compiled   :" $true
    Write-Host "Rollback firmware compiled  :" $true
    Write-Host "Current artifact count      :" $currentArtifacts.Count
    Write-Host "Rollback artifact count     :" $rollbackArtifacts.Count
    Write-Host "Current warning count       :" $currentWarnings.Count
    Write-Host "Rollback warning count      :" $rollbackWarnings.Count
    Write-Host "Secrets copied to evidence  :" $secretsCopiedToEvidence
    Write-Host "Repository unchanged        :" $repositoryUnchanged
    Write-Host "Firmware uploaded           :" $false
    Write-Host "Serial port opened          :" $false
    Write-Host "Physical state changed      :" $false
    Write-Host "Bundle manifest SHA-256     :" $bundleManifestHash
    Write-Host "Evidence SHA-256            :" $evidenceHash
    Write-Host ""
    Write-Host "The firmware bundle is sensitive because compiled binaries contain local configuration."
    Write-Host "No source or secret value was copied into retained evidence."
    Write-Host "No upload, board enumeration, serial-port open, reset, or physical mutation was performed."
}
finally
{
    if ($null -ne $workingRoot -and
        (Test-Path -LiteralPath $workingRoot -PathType Container))
    {
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
