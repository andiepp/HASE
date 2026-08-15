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
    [string]$ReadinessPlanPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-fA-F]{64}$")]
    [string]$ExpectedReadinessPlanSha256,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^COM[1-9][0-9]*$")]
    [string]$Port,

    [Parameter(Mandatory = $true)]
    [ValidatePattern("^VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4}$")]
    [string]$VendorProduct,

    [Parameter(Mandatory = $true)]
    [string]$UploadWorkingRoot,

    [Parameter(Mandatory = $true)]
    [string]$UploadEvidenceRoot
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
$uploadInvocationCount = 0

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

function Invoke-ArduinoCliInspection
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

function Invoke-SingleFirmwareUpload
{
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $script:uploadInvocationCount++

    if ($script:uploadInvocationCount -ne 1)
    {
        throw "More than one firmware-upload invocation was attempted."
    }

    return Invoke-ArduinoCliInspection -Arguments $Arguments
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

function Get-SelectedArtifactSignatures
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][object[]]$Artifacts
    )

    return @(
        foreach ($artifact in $Artifacts)
        {
            $relativePath = $artifact.name.Replace('/', '\')
            $path = Join-Path $Root $relativePath

            if (Test-Path -LiteralPath $path -PathType Leaf)
            {
                $hash = (Get-FileHash `
                    -LiteralPath $path `
                    -Algorithm SHA256).Hash.ToLowerInvariant()
                "{0}|{1}|{2}" -f `
                    $artifact.name, `
                    (Get-Item -LiteralPath $path).Length, `
                    $hash
            }
        }
    ) | Sort-Object
}

function Get-AdditionalArtifactRecords
{
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$ApprovedNames
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root)
    $prefix = $fullRoot.TrimEnd('\') + "\"

    return @(
        Get-ChildItem -LiteralPath $fullRoot -File -Recurse |
            ForEach-Object {
                $name = $_.FullName.Substring($prefix.Length).Replace('\', '/')

                if ($name -cnotin $ApprovedNames)
                {
                    $hash = (Get-FileHash `
                        -LiteralPath $_.FullName `
                        -Algorithm SHA256).Hash.ToLowerInvariant()
                    [ordered]@{
                        name = $name
                        length = $_.Length
                        sha256 = $hash
                    }
                }
            }
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

function Get-MatchingDevice
{
    $portPattern = "\({0}\)$" -f [regex]::Escape($Port)

    return @(
        Get-PnpDevice -Class Ports -PresentOnly -ErrorAction Stop |
            Where-Object {
                $_.FriendlyName -match $portPattern -and
                $_.InstanceId -match [regex]::Escape($VendorProduct) -and
                $_.Status -ceq "OK"
            }
    )
}

function Write-UploadEvidence
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][bool]$UploadSucceeded,
        [Parameter(Mandatory = $true)][bool]$PortReturned,
        [Parameter(Mandatory = $true)][bool]$OutcomeUncertain,
        [Parameter(Mandatory = $true)][bool]$RetainedBundleUnchanged,
        [Parameter(Mandatory = $true)][bool]$WorkingArtifactsUnchanged,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$UploaderGeneratedArtifacts
    )

    $document = [ordered]@{
        formatVersion = 1
        increment = "54E2B2"
        computer = $expectedComputer
        repositoryCommit = $ExpectedCommit
        bundleRepositoryCommit = $ExpectedBundleRepositoryCommit
        bundleManifestSha256 = $ExpectedBundleManifestSha256
        preparationEvidenceSha256 = $ExpectedPreparationEvidenceSha256
        readinessPlanSha256 = $ExpectedReadinessPlanSha256
        fqbn = $fqbn
        selectedPort = $Port
        vendorProduct = $VendorProduct
        uploadInvocationCount = $script:uploadInvocationCount
        uploadSucceeded = $UploadSucceeded
        portReturned = $PortReturned
        outcomeUncertain = $OutcomeUncertain
        automaticRetryAttempted = $false
        automaticRollbackAttempted = $false
        serialPortOpenedByUploader = $true
        physicalStateChanged = $true
        retainedBundleUnchanged = $RetainedBundleUnchanged
        uploadWorkspaceApprovedArtifactsUnchanged = $WorkingArtifactsUnchanged
        uploaderGeneratedArtifactCount = $UploaderGeneratedArtifacts.Count
        uploaderGeneratedArtifacts = @($UploaderGeneratedArtifacts)
        uploadWorkspaceRetained = $true
    }
    [System.IO.File]::WriteAllText(
        $Path,
        ($document | ConvertTo-Json -Depth 6),
        $utf8NoBom)
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$ArduinoCliPath = [System.IO.Path]::GetFullPath($ArduinoCliPath)
$BundleRoot = [System.IO.Path]::GetFullPath($BundleRoot)
$PreparationEvidencePath =
    [System.IO.Path]::GetFullPath($PreparationEvidencePath)
$ReadinessPlanPath = [System.IO.Path]::GetFullPath($ReadinessPlanPath)
$UploadWorkingRoot = [System.IO.Path]::GetFullPath($UploadWorkingRoot)
$UploadEvidenceRoot = [System.IO.Path]::GetFullPath($UploadEvidenceRoot)
$ExpectedCommit = $ExpectedCommit.ToLowerInvariant()
$ExpectedBundleRepositoryCommit =
    $ExpectedBundleRepositoryCommit.ToLowerInvariant()
$ExpectedBundleManifestSha256 =
    $ExpectedBundleManifestSha256.ToLowerInvariant()
$ExpectedPreparationEvidenceSha256 =
    $ExpectedPreparationEvidenceSha256.ToLowerInvariant()
$ExpectedReadinessPlanSha256 =
    $ExpectedReadinessPlanSha256.ToLowerInvariant()
$VendorProduct = $VendorProduct.ToUpperInvariant()

if ($env:COMPUTERNAME -cne $expectedComputer)
{
    throw "The controlled firmware upload must run on AEPRAKETE."
}

$repositoryPrefix = $RepositoryRoot.TrimEnd('\') + "\"
$localCustodyRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "HASE\Esp32DeploymentBundles"))
$localCustodyPrefix = $localCustodyRoot.TrimEnd('\') + "\"
$localWorkingCustodyRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "HASE\Esp32ControlledUploadWorkspaces"))
$localWorkingCustodyPrefix = $localWorkingCustodyRoot.TrimEnd('\') + "\"

if (-not $BundleRoot.StartsWith(
        $localCustodyPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The sensitive bundle is outside current-user local HASE custody."
}

if (-not $UploadWorkingRoot.StartsWith(
        $localWorkingCustodyPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The upload-working root is outside current-user local HASE custody."
}

if (Test-Path -LiteralPath $UploadWorkingRoot)
{
    throw "The upload-working root already exists."
}

if ($UploadEvidenceRoot.StartsWith(
        $repositoryPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The upload-evidence root must be outside the repository."
}

if (Test-Path -LiteralPath $UploadEvidenceRoot)
{
    throw "The upload-evidence root already exists."
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
    $origin.Count -ne 1 -or $origin[0].Trim() -cne $ExpectedCommit -or
    $branch.Count -ne 1 -or $branch[0].Trim() -cne "main" -or
    $statusBefore.Count -ne 0)
{
    throw "The controlled-upload repository state is invalid."
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
$versionResult = Invoke-ArduinoCliInspection -Arguments @("version")
$coreResult = Invoke-ArduinoCliInspection -Arguments @("core", "list")
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
    $PreparationEvidencePath,
    $ReadinessPlanPath))
{
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf))
    {
        throw "A controlled-upload custody file is missing."
    }
}

$actualManifestHash =
    (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).
        Hash.ToLowerInvariant()
$actualPreparationEvidenceHash =
    (Get-FileHash -LiteralPath $PreparationEvidencePath -Algorithm SHA256).
        Hash.ToLowerInvariant()
$actualReadinessPlanHash =
    (Get-FileHash -LiteralPath $ReadinessPlanPath -Algorithm SHA256).
        Hash.ToLowerInvariant()

if ($actualManifestHash -cne $ExpectedBundleManifestSha256 -or
    $actualPreparationEvidenceHash -cne $ExpectedPreparationEvidenceSha256 -or
    $actualReadinessPlanHash -cne $ExpectedReadinessPlanSha256)
{
    throw "A controlled-upload custody hash is incorrect."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$preparationEvidence =
    Get-Content -LiteralPath $PreparationEvidencePath -Raw | ConvertFrom-Json
$plan = Get-Content -LiteralPath $ReadinessPlanPath -Raw | ConvertFrom-Json

if ($manifest.formatVersion -ne 1 -or
    $manifest.increment -cne "54E2A" -or
    $manifest.repositoryCommit -cne $ExpectedBundleRepositoryCommit -or
    $manifest.fqbn -cne $fqbn -or
    $preparationEvidence.repositoryCommit -cne
        $ExpectedBundleRepositoryCommit -or
    $preparationEvidence.bundleManifestSha256 -cne
        $ExpectedBundleManifestSha256 -or
    $preparationEvidence.firmwareCompiled -ne $true -or
    $preparationEvidence.firmwareUploaded -ne $false -or
    $plan.formatVersion -ne 1 -or
    $plan.increment -cne "54E2B1" -or
    $plan.computer -cne $expectedComputer -or
    $plan.repositoryCommit -cne $ExpectedCommit -or
    $plan.bundleRepositoryCommit -cne $ExpectedBundleRepositoryCommit -or
    $plan.arduinoCliSha256 -cne $expectedCliHash -or
    $plan.arduinoCliVersion -cne $expectedCliVersion -or
    $plan.esp32CoreVersion -cne $expectedCoreVersion -or
    $plan.bundleManifestSha256 -cne $ExpectedBundleManifestSha256 -or
    $plan.preparationEvidenceSha256 -cne
        $ExpectedPreparationEvidenceSha256 -or
    $plan.selectedPort -cne $Port -or
    $plan.vendorProduct -cne $VendorProduct -or
    $plan.fqbn -cne $fqbn -or
    $plan.repositoryUnchanged -ne $true -or
    $plan.firmwareUploaded -ne $false -or
    $plan.serialPortOpened -ne $false -or
    $plan.physicalStateChanged -ne $false)
{
    throw "The controlled-upload readiness plan semantics are invalid."
}

$actualCurrent = @(Get-ActualArtifactSignatures -Root $currentRoot)
$manifestCurrent = @(
    Get-ManifestArtifactSignatures -Artifacts @($manifest.currentArtifacts))
$planCurrent = @(
    Get-ManifestArtifactSignatures -Artifacts @($plan.currentArtifacts))
$actualRollback = @(Get-ActualArtifactSignatures -Root $rollbackRoot)
$manifestRollback = @(
    Get-ManifestArtifactSignatures -Artifacts @($manifest.rollbackArtifacts))
$planRollback = @(
    Get-ManifestArtifactSignatures -Artifacts @($plan.rollbackArtifacts))

Assert-ExactStringSet -Expected $manifestCurrent -Actual $actualCurrent `
    -Description "current artifact"
Assert-ExactStringSet -Expected $manifestCurrent -Actual $planCurrent `
    -Description "planned current artifact"
Assert-ExactStringSet -Expected $manifestRollback -Actual $actualRollback `
    -Description "rollback artifact"
Assert-ExactStringSet -Expected $manifestRollback -Actual $planRollback `
    -Description "planned rollback artifact"

if (@(Get-MatchingDevice).Count -ne 1)
{
    throw "The selected ESP32 device identity is not ready."
}

[System.IO.Directory]::CreateDirectory($UploadWorkingRoot) | Out-Null
$workingPrefix = $UploadWorkingRoot.TrimEnd('\') + "\"

foreach ($artifact in @($manifest.currentArtifacts))
{
    $relativePath = $artifact.name.Replace('/', '\')
    $sourcePath = [System.IO.Path]::GetFullPath(
        (Join-Path $currentRoot $relativePath))
    $targetPath = [System.IO.Path]::GetFullPath(
        (Join-Path $UploadWorkingRoot $relativePath))

    if (-not $sourcePath.StartsWith(
            ($currentRoot.TrimEnd('\') + "\"),
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $targetPath.StartsWith(
            $workingPrefix,
            [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "An upload artifact path escaped its custody root."
    }

    $targetDirectory = Split-Path -Parent $targetPath
    [System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath
}

$workingBefore = @(Get-ActualArtifactSignatures -Root $UploadWorkingRoot)
Assert-ExactStringSet -Expected $manifestCurrent -Actual $workingBefore `
    -Description "upload-working artifact"

[System.IO.Directory]::CreateDirectory($UploadEvidenceRoot) | Out-Null
$beginPath = Join-Path $UploadEvidenceRoot "upload-begin.json"
$beginDocument = [ordered]@{
    formatVersion = 1
    increment = "54E2B2"
    repositoryCommit = $ExpectedCommit
    readinessPlanSha256 = $ExpectedReadinessPlanSha256
    uploadWorkspaceCreated = $true
    uploadInvocationCount = 0
    firmwareUploaded = $false
    serialPortOpenedByUploader = $false
    physicalStateChanged = $false
}
[System.IO.File]::WriteAllText(
    $beginPath,
    ($beginDocument | ConvertTo-Json -Depth 4),
    $utf8NoBom)

$uploadArguments = @(
    "upload",
    "--fqbn", $fqbn,
    "--port", $Port,
    "--input-dir", $UploadWorkingRoot
)
$uploadResult = Invoke-SingleFirmwareUpload -Arguments $uploadArguments
$resultPath = Join-Path $UploadEvidenceRoot "upload-result.json"

$currentAfter = @(Get-ActualArtifactSignatures -Root $currentRoot)
$rollbackAfter = @(Get-ActualArtifactSignatures -Root $rollbackRoot)
$workingApprovedAfter = @(
    Get-SelectedArtifactSignatures `
        -Root $UploadWorkingRoot `
        -Artifacts @($manifest.currentArtifacts))
$approvedNames = @($manifest.currentArtifacts | ForEach-Object { $_.name })
$uploaderGeneratedArtifacts = @(
    Get-AdditionalArtifactRecords `
        -Root $UploadWorkingRoot `
        -ApprovedNames $approvedNames)
$retainedBundleUnchanged =
    @(Compare-Object -ReferenceObject $manifestCurrent `
        -DifferenceObject $currentAfter -CaseSensitive).Count -eq 0 -and
    @(Compare-Object -ReferenceObject $manifestRollback `
        -DifferenceObject $rollbackAfter -CaseSensitive).Count -eq 0
$workingArtifactsUnchanged =
    @(Compare-Object -ReferenceObject $manifestCurrent `
        -DifferenceObject $workingApprovedAfter -CaseSensitive).Count -eq 0

if ($uploadResult.ExitCode -ne 0)
{
    Write-UploadEvidence `
        -Path $resultPath `
        -UploadSucceeded $false `
        -PortReturned $false `
        -OutcomeUncertain $true `
        -RetainedBundleUnchanged $retainedBundleUnchanged `
        -WorkingArtifactsUnchanged $workingArtifactsUnchanged `
        -UploaderGeneratedArtifacts $uploaderGeneratedArtifacts
    throw "The single controlled firmware upload failed; outcome is uncertain."
}

$portReturned = $false

for ($attempt = 1; $attempt -le 30; $attempt++)
{
    if (@(Get-MatchingDevice).Count -eq 1)
    {
        $portReturned = $true
        break
    }

    Start-Sleep -Seconds 1
}

Write-UploadEvidence `
    -Path $resultPath `
    -UploadSucceeded $true `
    -PortReturned $portReturned `
    -OutcomeUncertain $false `
    -RetainedBundleUnchanged $retainedBundleUnchanged `
    -WorkingArtifactsUnchanged $workingArtifactsUnchanged `
    -UploaderGeneratedArtifacts $uploaderGeneratedArtifacts

if (-not $retainedBundleUnchanged -or -not $workingArtifactsUnchanged)
{
    throw "The upload succeeded but firmware artifact custody changed."
}

if (-not $portReturned)
{
    throw "The upload succeeded but the exact ESP32 port identity did not return."
}

$statusAfter = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)
Assert-ExactStringSet -Expected $statusBefore -Actual $statusAfter `
    -Description "repository status"

$beginHash =
    (Get-FileHash -LiteralPath $beginPath -Algorithm SHA256).
        Hash.ToLowerInvariant()
$resultHash =
    (Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).
        Hash.ToLowerInvariant()

Write-Host ""
Write-Host "ADR-0054 Increment 54E2B2 controlled upload"
Write-Host "Upload invocation count     :" $uploadInvocationCount
Write-Host "Upload succeeded            :" $true
Write-Host "Exact device returned       :" $portReturned
Write-Host "Automatic retry attempted   :" $false
Write-Host "Automatic rollback attempted:" $false
Write-Host "Repository unchanged        :" $true
Write-Host "Retained bundle unchanged   :" $retainedBundleUnchanged
Write-Host "Working artifacts unchanged :" $workingArtifactsUnchanged
Write-Host "Uploader-generated artifacts:" $uploaderGeneratedArtifacts.Count
Write-Host "Upload workspace retained   :" $true
Write-Host "Serial port opened by uploader:" $true
Write-Host "Physical state changed      :" $true
Write-Host "Begin evidence SHA-256      :" $beginHash
Write-Host "Result evidence SHA-256     :" $resultHash
Write-Host ""
Write-Host "No Runtime Host, Client, protocol, or endpoint-behavior validation was performed."
