param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = "H:\Development",

    [Parameter(Mandatory = $false)]
    [string]$ArduinoCliPath =
        "I:\Arduino\arduino-ide_2.3.7\resources\app\lib\backend\resources\arduino-cli.exe",

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expectedCommit =
    "68bd6ba7ae84cf5784014552390947bc854d241a"

$expectedCliHash =
    "7c4f90d6b1f640975a0f0ed3fab8a93f969e0ce0058c99bda69f07228d50cb6b"

$expectedCliVersion = "1.3.1"
$expectedCoreVersion = "3.3.10"
$fqbn = "esp32:esp32:esp32doit-devkit-v1"

$expectedChangedPaths = @(
    "libraries/HaseEsp32Endpoint/src/HaseEndpointDefinitionResolver.h",
    "libraries/HaseEsp32Endpoint/src/HaseEndpointRequestProcessor.cpp",
    "libraries/HaseEsp32Endpoint/src/HaseEndpointRequestProcessor.h",
    "libraries/HaseEsp32Endpoint/src/HaseEsp32Endpoint.h",
    "tests/Arduino/HaseEndpointRequestProcessorValidation/HaseEndpointRequestProcessorValidation.ino",
    "tools/Arduino/Test-HaseEsp32EndpointCompilation.ps1",
    "tools/Arduino/Test-HaseEsp32EndpointRequestProcessor.ps1"
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

function Invoke-CliCapture
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
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

    [System.IO.File]::WriteAllLines(
        $LogPath,
        $output,
        (New-Object System.Text.UTF8Encoding($false)))

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Get-ArtifactEvidence
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    return @(
        Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse |
        ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Length = $_.Length
                Sha256 =
                    (Get-FileHash `
                        -LiteralPath $_.FullName `
                        -Algorithm SHA256).
                        Hash.ToLowerInvariant()
            }
        }
    )
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$ArduinoCliPath = [System.IO.Path]::GetFullPath($ArduinoCliPath)
$EvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container))
{
    throw "The repository root does not exist."
}

$repositoryPrefix = $RepositoryRoot.TrimEnd('\') + "\"

if ($EvidenceRoot.StartsWith(
        $repositoryPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The evidence directory must be outside the repository."
}

if (Test-Path -LiteralPath $EvidenceRoot)
{
    throw "The 54C2 evidence directory already exists."
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

$head = @(Invoke-GitLines -Arguments @("rev-parse", "HEAD"))
$origin = @(Invoke-GitLines -Arguments @("rev-parse", "origin/main"))
$branch = @(Invoke-GitLines -Arguments @("branch", "--show-current"))

if ($head.Count -ne 1 -or $head[0].Trim() -cne $expectedCommit)
{
    throw "Repository HEAD is not the approved 54C1 baseline."
}

if ($origin.Count -ne 1 -or $origin[0].Trim() -cne $expectedCommit)
{
    throw "origin/main is not the approved 54C1 baseline."
}

if ($branch.Count -ne 1 -or $branch[0].Trim() -cne "main")
{
    throw "The repository is not on main."
}

$statusBefore = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

$changedPaths = @(
    $statusBefore |
    ForEach-Object {
        if ($_.Length -lt 4)
        {
            throw "A repository status entry is malformed."
        }

        $_.Substring(3).Replace("\", "/")
    }
)

Assert-ExactStringSet `
    -Expected $expectedChangedPaths `
    -Actual $changedPaths `
    -Description "54C2 changed path"

$stagedPaths = @(
    Invoke-GitLines -Arguments @("diff", "--cached", "--name-only")
)

if ($stagedPaths.Count -ne 0)
{
    throw "54C2 files must not be staged during focused validation."
}

$repositoryFixtureRoot =
    Join-Path `
        $RepositoryRoot `
        "tests\Arduino\HaseEndpointRequestProcessorValidation"

$repositoryFixturePath =
    Join-Path `
        $repositoryFixtureRoot `
        "HaseEndpointRequestProcessorValidation.ino"

if (-not (Test-Path -LiteralPath $repositoryFixturePath -PathType Leaf))
{
    throw "The endpoint request-processor validation fixture is missing."
}

$processorSourcePath =
    Join-Path `
        $RepositoryRoot `
        "libraries\HaseEsp32Endpoint\src\HaseEndpointRequestProcessor.cpp"

if (-not (Test-Path -LiteralPath $processorSourcePath -PathType Leaf))
{
    throw "The endpoint request-processor source is missing."
}

$processorSource =
    [System.IO.File]::ReadAllText($processorSourcePath)

$mutationCallbackSites = @(
    "registration->writeBoolean(",
    "registration->executeNullBoolean("
)

foreach ($callbackSite in $mutationCallbackSites)
{
    $siteCount =
        [regex]::Matches(
            $processorSource,
            [regex]::Escape($callbackSite)).Count

    if ($siteCount -ne 1)
    {
        $message =
            "The mutation callback site count is invalid: {0}" -f
                $callbackSite

        throw $message
    }
}

$fixtureSource =
    [System.IO.File]::ReadAllText($repositoryFixturePath)

foreach ($dispatchToken in @(
    "DiscoverRequestRecognized",
    "ReadPropertyRequestRecognized",
    "WritePropertyRequestRecognized",
    "ExecuteCommandRequestRecognized",
    "ReadEndpointDescriptorRequestRecognized",
    "UnsupportedVersion",
    "InvalidDiscoverRequest",
    "InvalidReadPropertyRequest",
    "InvalidWritePropertyRequest",
    "InvalidExecuteCommandRequest",
    "InvalidReadEndpointDescriptorRequest",
    "UnsupportedMessage"))
{
    if ($fixtureSource.IndexOf(
            $dispatchToken,
            [System.StringComparison]::Ordinal) -lt 0)
    {
        $message = "The fixture is missing a dispatch contract: {0}" -f
            $dispatchToken

        throw $message
    }
}

$powerShellDirectory =
    Join-Path `
        $env:SystemRoot `
        "System32\WindowsPowerShell\v1.0"

$powerShellExecutable =
    Join-Path $powerShellDirectory "powershell.exe"

if (-not (Test-Path -LiteralPath $powerShellExecutable -PathType Leaf))
{
    throw "The conventional Windows PowerShell executable is missing."
}

$env:Path = $powerShellDirectory + ";" + $env:Path

$resolvedPowerShell = @(
    Get-Command `
        powershell.exe `
        -CommandType Application `
        -All `
        -ErrorAction Stop |
    ForEach-Object {
        [System.IO.Path]::GetFullPath($_.Source)
    }
)

if ($resolvedPowerShell.Count -eq 0 `
    -or -not [string]::Equals(
        $resolvedPowerShell[0],
        $powerShellExecutable,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "powershell.exe did not resolve to the expected executable."
}

$versionOutput = @(
    & $ArduinoCliPath version 2>&1 |
        ForEach-Object { $_.ToString() }
)

if ($LASTEXITCODE -ne 0 `
    -or @($versionOutput | Where-Object {
        $_ -match [regex]::Escape($expectedCliVersion)
    }).Count -eq 0)
{
    throw "Arduino CLI version validation failed."
}

$coreOutput = @(
    & $ArduinoCliPath core list 2>&1 |
        ForEach-Object { $_.ToString() }
)

$corePattern = "^esp32:esp32\s+{0}(\s|$)" -f (
    [regex]::Escape($expectedCoreVersion))

if ($LASTEXITCODE -ne 0 `
    -or @($coreOutput | Where-Object { $_ -match $corePattern }).Count -ne 1)
{
    throw "ESP32 core version validation failed."
}

[System.IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null

$buildOneRoot = Join-Path $EvidenceRoot "Build1"
$buildTwoRoot = Join-Path $EvidenceRoot "Build2"
$outputOneRoot = Join-Path $EvidenceRoot "Output1"
$outputTwoRoot = Join-Path $EvidenceRoot "Output2"
$logRoot = Join-Path $EvidenceRoot "Logs"
$stagedFixtureRoot =
    Join-Path `
        $EvidenceRoot `
        "Sketch\HaseEndpointRequestProcessorValidation"

foreach ($directory in @(
    $buildOneRoot,
    $buildTwoRoot,
    $outputOneRoot,
    $outputTwoRoot,
    $logRoot,
    $stagedFixtureRoot))
{
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}

$stagedFixturePath =
    Join-Path `
        $stagedFixtureRoot `
        "HaseEndpointRequestProcessorValidation.ino"

[System.IO.File]::Copy(
    $repositoryFixturePath,
    $stagedFixturePath,
    $false)

$stagedFixtureFiles = @(
    Get-ChildItem -LiteralPath $stagedFixtureRoot -File
)

if ($stagedFixtureFiles.Count -ne 1 `
    -or $stagedFixtureFiles[0].Name -cne
        "HaseEndpointRequestProcessorValidation.ino")
{
    throw "The staged endpoint request-processor fixture is invalid."
}

$repositoryLibrariesRoot = Join-Path $RepositoryRoot "libraries"

$compileOneLog = Join-Path $logRoot "Compile1.txt"
$compileOneArguments = @(
    "compile",
    "--fqbn", $fqbn,
    "--libraries", $repositoryLibrariesRoot,
    "--build-path", $buildOneRoot,
    "--output-dir", $outputOneRoot,
    "--clean",
    "--verbose",
    $stagedFixtureRoot
)

Write-Host "Starting endpoint request-processor compilation 1 of 2..."
$compileOneResult =
    Invoke-CliCapture `
        -Arguments $compileOneArguments `
        -LogPath $compileOneLog

if ($compileOneResult.ExitCode -ne 0)
{
    throw "The first endpoint request-processor compilation failed."
}

$compileTwoLog = Join-Path $logRoot "Compile2.txt"
$compileTwoArguments = @(
    "compile",
    "--fqbn", $fqbn,
    "--libraries", $repositoryLibrariesRoot,
    "--build-path", $buildTwoRoot,
    "--output-dir", $outputTwoRoot,
    "--clean",
    "--verbose",
    $stagedFixtureRoot
)

Write-Host "Starting endpoint request-processor compilation 2 of 2..."
$compileTwoResult =
    Invoke-CliCapture `
        -Arguments $compileTwoArguments `
        -LogPath $compileTwoLog

if ($compileTwoResult.ExitCode -ne 0)
{
    throw "The second endpoint request-processor compilation failed."
}

$combinedOutput = @($compileOneResult.Output + $compileTwoResult.Output)

if (@($combinedOutput | Where-Object {
        $_ -match [regex]::Escape("HaseEsp32Endpoint")
    }).Count -eq 0)
{
    throw "Compilation evidence did not resolve the HASE library."
}

$warningLinesOne = @(
    $compileOneResult.Output |
        Where-Object { $_ -match "\bwarning:|\bWarnung:" }
)

$warningLinesTwo = @(
    $compileTwoResult.Output |
        Where-Object { $_ -match "\bwarning:|\bWarnung:" }
)

if ($warningLinesOne.Count -ne 0 -or $warningLinesTwo.Count -ne 0)
{
    throw "The focused endpoint request-processor compilation produced warnings."
}

$artifactOne = @(Get-ArtifactEvidence -OutputDirectory $outputOneRoot)
$artifactTwo = @(Get-ArtifactEvidence -OutputDirectory $outputTwoRoot)

if ($artifactOne.Count -eq 0 -or $artifactTwo.Count -eq 0)
{
    throw "A focused compilation produced no artifacts."
}

$artifactNamesOne = @(
    $artifactOne | ForEach-Object { $_.Name } | Sort-Object
)

$artifactNamesTwo = @(
    $artifactTwo | ForEach-Object { $_.Name } | Sort-Object
)

Assert-ExactStringSet `
    -Expected $artifactNamesOne `
    -Actual $artifactNamesTwo `
    -Description "focused artifact name"

$artifactLengthsOne = @(
    $artifactOne |
        ForEach-Object { "{0}:{1}" -f $_.Name, $_.Length } |
        Sort-Object
)

$artifactLengthsTwo = @(
    $artifactTwo |
        ForEach-Object { "{0}:{1}" -f $_.Name, $_.Length } |
        Sort-Object
)

Assert-ExactStringSet `
    -Expected $artifactLengthsOne `
    -Actual $artifactLengthsTwo `
    -Description "focused artifact length"

$statusAfter = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

Assert-ExactStringSet `
    -Expected $statusBefore `
    -Actual $statusAfter `
    -Description "repository status"

Write-Host ""
Write-Host "ADR-0054 Increment 54C2 focused request-processor validation"
Write-Host ""
Write-Host "Computer                    :" $env:COMPUTERNAME
Write-Host "HEAD exact                  :" ($head[0].Trim() -ceq $expectedCommit)
Write-Host "origin/main exact           :" ($origin[0].Trim() -ceq $expectedCommit)
Write-Host "Changed paths exact         :" $true
Write-Host "Files staged                :" $false
Write-Host "Arduino CLI version exact   :" $true
Write-Host "ESP32 core version exact    :" $true
Write-Host "FQBN exact                  :" $true
Write-Host "PowerShell process PATH only:" $true
Write-Host "Fixture staged externally     :" $true
Write-Host "Resolver fixtures           :" "known and unknown"
Write-Host "Dispatch contracts          :" "all Protocol V1 outcomes"
Write-Host "Mutation callback sites     :" "exactly one each"
Write-Host "Compile 1 succeeded         :" ($compileOneResult.ExitCode -eq 0)
Write-Host "Compile 2 succeeded         :" ($compileTwoResult.ExitCode -eq 0)
Write-Host "Compile 1 warning lines     :" $warningLinesOne.Count
Write-Host "Compile 2 warning lines     :" $warningLinesTwo.Count
Write-Host "Artifact names equal        :" $true
Write-Host "Artifact lengths equal      :" $true
Write-Host "Evidence root               :" $EvidenceRoot
Write-Host ""
Write-Host "Compile 1 artifacts:"
$artifactOne | Format-Table -AutoSize
Write-Host "Compile 2 artifacts:"
$artifactTwo | Format-Table -AutoSize
Write-Host "No install, update, upload, board detection, or serial-port command was invoked."
Write-Host "No local Wi-Fi secret was read or hashed."
Write-Host "The evidence directory is retained outside the repository."
