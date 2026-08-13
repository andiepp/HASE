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

$expectedCliHash =
    "7c4f90d6b1f640975a0f0ed3fab8a93f969e0ce0058c99bda69f07228d50cb6b"

$expectedCliVersion = "1.3.1"
$expectedCoreVersion = "3.3.10"
$fqbn = "esp32:esp32:esp32doit-devkit-v1"

$expectedApplicationFiles = @(
    "HaseBme280Sensor.cpp",
    "HaseBme280Sensor.h",
    "HaseEndpoint.ino",
    "HasePhysicalEndpointDescriptor.cpp",
    "HasePhysicalEndpointDescriptor.h",
    "HasePhysicalEventPublisher.cpp",
    "HasePhysicalEventPublisher.h",
    "HasePhysicalExecuteCommandHandler.cpp",
    "HasePhysicalExecuteCommandHandler.h",
    "HasePhysicalPropertyService.cpp",
    "HasePhysicalPropertyService.h",
    "HasePhysicalReadPropertyHandler.cpp",
    "HasePhysicalReadPropertyHandler.h",
    "HasePhysicalWritePropertyHandler.cpp",
    "HasePhysicalWritePropertyHandler.h",
    "HasePushButton.cpp",
    "HasePushButton.h",
    "HaseSecrets.example.h",
    "HaseStatusLed.cpp",
    "HaseStatusLed.h"
) | Sort-Object

$expectedFrameworkFiles = @(
    "HaseBinaryProtocolReader.cpp",
    "HaseBinaryProtocolReader.h",
    "HaseBinaryProtocolWriter.cpp",
    "HaseBinaryProtocolWriter.h",
    "HaseCommandDescriptorSerializer.cpp",
    "HaseCommandDescriptorSerializer.h",
    "HaseDataDescriptorSerializer.cpp",
    "HaseDataDescriptorSerializer.h",
    "HaseDescriptorModel.cpp",
    "HaseDescriptorModel.h",
    "HaseDiscoverHandler.cpp",
    "HaseDiscoverHandler.h",
    "HaseEndpointDescriptorSerializer.cpp",
    "HaseEndpointDescriptorSerializer.h",
    "HaseEndpointMetadataSerializer.cpp",
    "HaseEndpointMetadataSerializer.h",
    "HaseEsp32Endpoint.h",
    "HaseEventDescriptorSerializer.cpp",
    "HaseEventDescriptorSerializer.h",
    "HaseEventNotificationHandler.cpp",
    "HaseEventNotificationHandler.h",
    "HaseExecuteCommandRequest.cpp",
    "HaseExecuteCommandRequest.h",
    "HaseExecuteCommandResponseHandler.cpp",
    "HaseExecuteCommandResponseHandler.h",
    "HaseInstrumentDescriptorSerializer.cpp",
    "HaseInstrumentDescriptorSerializer.h",
    "HaseInstrumentMetadataSerializer.cpp",
    "HaseInstrumentMetadataSerializer.h",
    "HaseMdnsAdvertiser.cpp",
    "HaseMdnsAdvertiser.h",
    "HasePropertyDescriptorSerializer.cpp",
    "HasePropertyDescriptorSerializer.h",
    "HaseProtocolDispatcher.cpp",
    "HaseProtocolDispatcher.h",
    "HaseProtocolEnvelope.cpp",
    "HaseProtocolEnvelope.h",
    "HaseProtocolSerializationHelper.cpp",
    "HaseProtocolSerializationHelper.h",
    "HaseReadEndpointDescriptorHandler.cpp",
    "HaseReadEndpointDescriptorHandler.h",
    "HaseReadPropertyRequest.cpp",
    "HaseReadPropertyRequest.h",
    "HaseReadPropertyResponseHandler.cpp",
    "HaseReadPropertyResponseHandler.h",
    "HaseTcpTransport.cpp",
    "HaseTcpTransport.h",
    "HaseUtcClock.cpp",
    "HaseUtcClock.h",
    "HaseWritePropertyRequest.cpp",
    "HaseWritePropertyRequest.h",
    "HaseWritePropertyResponseHandler.cpp",
    "HaseWritePropertyResponseHandler.h"
) | Sort-Object

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
        LogPath = $LogPath
    }
}

function Get-RequiredPropertyValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $prefix = $Name + "="
    $matches = @($Lines | Where-Object { $_.StartsWith($prefix) })

    if ($matches.Count -ne 1)
    {
        $message = "Expected one expanded build property: {0}" -f $Name
        throw $message
    }

    return $matches[0].Substring($prefix.Length).Trim().Trim('"')
}

function Get-ArtifactEvidence
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory
    )

    $files = @(Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse)

    if ($files.Count -eq 0)
    {
        throw "A compilation produced no output artifacts."
    }

    return @(
        foreach ($file in $files)
        {
            [pscustomobject]@{
                Name = $file.Name
                Length = $file.Length
                Sha256 =
                    (Get-FileHash `
                        -LiteralPath $file.FullName `
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
    throw "The evidence directory already exists."
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

if ($resolvedPowerShell.Count -eq 0)
{
    throw "powershell.exe does not resolve after the process-local correction."
}

if (-not [string]::Equals(
        $resolvedPowerShell[0],
        $powerShellExecutable,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "powershell.exe resolved to an unexpected executable."
}

$statusBefore = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

$localSecretsPath =
    Join-Path $RepositoryRoot "HaseEndpoint\HaseSecrets.h"

& git -C $RepositoryRoot check-ignore --quiet -- "HaseEndpoint/HaseSecrets.h"

if ($LASTEXITCODE -ne 0)
{
    throw "The local HaseSecrets.h path is not ignored."
}

$trackedSecrets = @(
    & git -C $RepositoryRoot ls-files -- "HaseEndpoint/HaseSecrets.h"
)

if ($LASTEXITCODE -ne 0)
{
    throw "Could not inspect local-secret tracking state."
}

if ($trackedSecrets.Count -ne 0)
{
    throw "The local HaseSecrets.h file is unexpectedly tracked."
}

$applicationRoot = Join-Path $RepositoryRoot "HaseEndpoint"
$frameworkLibraryRoot =
    Join-Path $RepositoryRoot "libraries\HaseEsp32Endpoint"
$frameworkSourceRoot = Join-Path $frameworkLibraryRoot "src"
$repositoryLibrariesRoot = Join-Path $RepositoryRoot "libraries"
$vendorLibrariesRoot = Join-Path $applicationRoot "Libraries"

$actualApplicationFiles = @(
    Get-ChildItem -LiteralPath $applicationRoot -File |
    Where-Object {
        $_.Extension -in @(".ino", ".cpp", ".h") `
            -and $_.Name -cne "HaseSecrets.h"
    } |
    ForEach-Object { $_.Name } |
    Sort-Object
)

Assert-ExactStringSet `
    -Expected $expectedApplicationFiles `
    -Actual $actualApplicationFiles `
    -Description "application source file"

$actualFrameworkFiles = @(
    Get-ChildItem -LiteralPath $frameworkSourceRoot -File |
    Where-Object { $_.Extension -in @(".cpp", ".h") } |
    ForEach-Object { $_.Name } |
    Sort-Object
)

Assert-ExactStringSet `
    -Expected $expectedFrameworkFiles `
    -Actual $actualFrameworkFiles `
    -Description "framework library source file"

$libraryPropertiesPath =
    Join-Path $frameworkLibraryRoot "library.properties"

$libraryProperties = @(Get-Content -LiteralPath $libraryPropertiesPath)

foreach ($requiredLine in @(
    "name=HASE ESP32 Endpoint",
    "version=0.1.0",
    "architectures=esp32",
    "includes=HaseEsp32Endpoint.h"))
{
    if (@($libraryProperties | Where-Object { $_ -ceq $requiredLine }).Count -ne 1)
    {
        $message = "The framework library metadata is missing: {0}" -f $requiredLine
        throw $message
    }
}

$requiredVendorLibraries = @(
    [pscustomobject]@{
        Directory = "Adafruit_BME280_Library"
        Name = "Adafruit BME280 Library"
        Version = "2.3.0"
        CompileReferenceRequired = $true
    },
    [pscustomobject]@{
        Directory = "Adafruit_BMP280_Library"
        Name = "Adafruit BMP280 Library"
        Version = "3.0.0"
        CompileReferenceRequired = $false
    },
    [pscustomobject]@{
        Directory = "Adafruit_BusIO"
        Name = "Adafruit BusIO"
        Version = "1.17.4"
        CompileReferenceRequired = $true
    },
    [pscustomobject]@{
        Directory = "Adafruit_Unified_Sensor"
        Name = "Adafruit Unified Sensor"
        Version = "1.1.15"
        CompileReferenceRequired = $true
    }
)

foreach ($library in $requiredVendorLibraries)
{
    $libraryPath = Join-Path $vendorLibrariesRoot $library.Directory
    $propertiesPath = Join-Path $libraryPath "library.properties"

    if (-not (Test-Path -LiteralPath $propertiesPath -PathType Leaf))
    {
        $message = "A required vendor library is missing: {0}" -f $library.Directory
        throw $message
    }

    $properties = @(Get-Content -LiteralPath $propertiesPath)
    $nameLine = "name={0}" -f $library.Name
    $versionLine = "version={0}" -f $library.Version

    if (@($properties | Where-Object { $_ -ceq $nameLine }).Count -ne 1 `
        -or @($properties | Where-Object { $_ -ceq $versionLine }).Count -ne 1)
    {
        $message = "A vendor library identity changed: {0}" -f $library.Directory
        throw $message
    }
}

$versionOutput = @(
    & $ArduinoCliPath version 2>&1 |
        ForEach-Object { $_.ToString() }
)

if ($LASTEXITCODE -ne 0)
{
    throw "Arduino CLI version inspection failed."
}

if (@($versionOutput | Where-Object {
        $_ -match [regex]::Escape($expectedCliVersion)
    }).Count -eq 0)
{
    throw "Arduino CLI version does not match the approved version."
}

$coreOutput = @(
    & $ArduinoCliPath core list 2>&1 |
        ForEach-Object { $_.ToString() }
)

if ($LASTEXITCODE -ne 0)
{
    throw "Arduino core inspection failed."
}

$corePattern = "^esp32:esp32\s+{0}(\s|$)" -f (
    [regex]::Escape($expectedCoreVersion))

if (@($coreOutput | Where-Object { $_ -match $corePattern }).Count -ne 1)
{
    throw "The approved ESP32 core version is not installed exactly once."
}

$boardOutput = @(
    & $ArduinoCliPath board listall 2>&1 |
        ForEach-Object { $_.ToString() }
)

if ($LASTEXITCODE -ne 0)
{
    throw "Arduino board-definition inspection failed."
}

if (@($boardOutput | Where-Object {
        $_ -match [regex]::Escape($fqbn)
    }).Count -ne 1)
{
    throw "The approved board definition is not available exactly once."
}

[System.IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null

$sketchRoot = Join-Path $EvidenceRoot "Sketch\HaseEndpoint"
$buildPropertiesRoot = Join-Path $EvidenceRoot "BuildProperties"
$buildOneRoot = Join-Path $EvidenceRoot "Build1"
$buildTwoRoot = Join-Path $EvidenceRoot "Build2"
$outputOneRoot = Join-Path $EvidenceRoot "Output1"
$outputTwoRoot = Join-Path $EvidenceRoot "Output2"
$logRoot = Join-Path $EvidenceRoot "Logs"

foreach ($directory in @(
    $sketchRoot,
    $buildPropertiesRoot,
    $buildOneRoot,
    $buildTwoRoot,
    $outputOneRoot,
    $outputTwoRoot,
    $logRoot))
{
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}

foreach ($applicationFile in $expectedApplicationFiles)
{
    [System.IO.File]::Copy(
        (Join-Path $applicationRoot $applicationFile),
        (Join-Path $sketchRoot $applicationFile),
        $false)
}

$placeholderSecretsPath = Join-Path $sketchRoot "HaseSecrets.h"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$placeholderLines = @(
    "#pragma once",
    "",
    "const char* WIFI_SSID =",
    "    `"HASE_COMPILE_VALIDATION_SSID`";",
    "",
    "const char* WIFI_PASSWORD =",
    "    `"HASE_COMPILE_VALIDATION_PASSWORD`";"
)

[System.IO.File]::WriteAllLines(
    $placeholderSecretsPath,
    $placeholderLines,
    $utf8NoBom)

$stagedFiles = @(Get-ChildItem -LiteralPath $sketchRoot -File)
$stagedInoCount = @($stagedFiles | Where-Object { $_.Extension -ceq ".ino" }).Count
$stagedCppCount = @($stagedFiles | Where-Object { $_.Extension -ceq ".cpp" }).Count
$stagedHeaderCount = @($stagedFiles | Where-Object { $_.Extension -ceq ".h" }).Count

if ($stagedFiles.Count -ne 21 `
    -or $stagedInoCount -ne 1 `
    -or $stagedCppCount -ne 9 `
    -or $stagedHeaderCount -ne 11)
{
    throw "The staged application file set is invalid."
}

$propertiesLog = Join-Path $logRoot "BuildProperties.txt"
$propertiesArguments = @(
    "compile",
    "--fqbn", $fqbn,
    "--libraries", $repositoryLibrariesRoot,
    "--libraries", $vendorLibrariesRoot,
    "--build-path", $buildPropertiesRoot,
    "--show-properties=expanded",
    $sketchRoot
)

Write-Host "Inspecting expanded ESP32 build properties..."
$propertiesResult =
    Invoke-CliCapture `
        -Arguments $propertiesArguments `
        -LogPath $propertiesLog

if ($propertiesResult.ExitCode -ne 0)
{
    throw "Expanded build-property discovery failed."
}

$buildFqbn =
    Get-RequiredPropertyValue `
        -Lines $propertiesResult.Output `
        -Name "build.fqbn"

$platformPath =
    Get-RequiredPropertyValue `
        -Lines $propertiesResult.Output `
        -Name "runtime.platform.path"

$compilerPath =
    Get-RequiredPropertyValue `
        -Lines $propertiesResult.Output `
        -Name "compiler.path"

$compilerCommand =
    Get-RequiredPropertyValue `
        -Lines $propertiesResult.Output `
        -Name "compiler.cpp.cmd"

if ($buildFqbn -cne $fqbn)
{
    throw "Expanded build properties selected a different FQBN."
}

$normalizedPlatformPath = $platformPath.TrimEnd('\', '/')

if (-not $normalizedPlatformPath.EndsWith(
        $expectedCoreVersion,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Expanded build properties selected a different ESP32 core."
}

$compilerExecutable = Join-Path $compilerPath $compilerCommand

if (-not (Test-Path -LiteralPath $compilerExecutable -PathType Leaf))
{
    $compilerExecutable = $compilerExecutable + ".exe"
}

if (-not (Test-Path -LiteralPath $compilerExecutable -PathType Leaf))
{
    throw "The expanded C++ compiler executable could not be resolved."
}

$compilerVersion = @(
    & $compilerExecutable --version 2>&1 |
        ForEach-Object { $_.ToString() }
)

if ($LASTEXITCODE -ne 0)
{
    throw "The selected C++ compiler version command failed."
}

$compileOneLog = Join-Path $logRoot "Compile1.txt"
$compileOneArguments = @(
    "compile",
    "--fqbn", $fqbn,
    "--libraries", $repositoryLibrariesRoot,
    "--libraries", $vendorLibrariesRoot,
    "--build-path", $buildOneRoot,
    "--output-dir", $outputOneRoot,
    "--clean",
    "--verbose",
    $sketchRoot
)

Write-Host "Starting clean ESP32 compilation 1 of 2..."
$compileOneResult =
    Invoke-CliCapture `
        -Arguments $compileOneArguments `
        -LogPath $compileOneLog

if ($compileOneResult.ExitCode -ne 0)
{
    throw "The first clean library-based compilation failed."
}

Write-Host "Clean ESP32 compilation 1 of 2 succeeded."

$compileTwoLog = Join-Path $logRoot "Compile2.txt"
$compileTwoArguments = @(
    "compile",
    "--fqbn", $fqbn,
    "--libraries", $repositoryLibrariesRoot,
    "--libraries", $vendorLibrariesRoot,
    "--build-path", $buildTwoRoot,
    "--output-dir", $outputTwoRoot,
    "--clean",
    "--verbose",
    $sketchRoot
)

Write-Host "Starting clean ESP32 compilation 2 of 2..."
$compileTwoResult =
    Invoke-CliCapture `
        -Arguments $compileTwoArguments `
        -LogPath $compileTwoLog

if ($compileTwoResult.ExitCode -ne 0)
{
    throw "The second clean library-based compilation failed."
}

Write-Host "Clean ESP32 compilation 2 of 2 succeeded."

$combinedCompileOutput =
    @($compileOneResult.Output + $compileTwoResult.Output)

foreach ($requiredReference in @(
    "HaseEsp32Endpoint",
    "Adafruit_BME280_Library",
    "Adafruit_BusIO",
    "Adafruit_Unified_Sensor"))
{
    if (@($combinedCompileOutput | Where-Object {
            $_ -match [regex]::Escape($requiredReference)
        }).Count -eq 0)
    {
        $message =
            "Compilation evidence did not reference required library: {0}" -f
                $requiredReference

        throw $message
    }
}

$artifactOne = @(Get-ArtifactEvidence -OutputDirectory $outputOneRoot)
$artifactTwo = @(Get-ArtifactEvidence -OutputDirectory $outputTwoRoot)

$artifactNamesOne = @(
    $artifactOne | ForEach-Object { $_.Name } | Sort-Object
)

$artifactNamesTwo = @(
    $artifactTwo | ForEach-Object { $_.Name } | Sort-Object
)

Assert-ExactStringSet `
    -Expected $artifactNamesOne `
    -Actual $artifactNamesTwo `
    -Description "clean-compilation artifact name"

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
    -Description "clean-compilation artifact length"

$artifactHashesOne = @(
    $artifactOne |
        ForEach-Object { "{0}:{1}" -f $_.Name, $_.Sha256 } |
        Sort-Object
)

$artifactHashesTwo = @(
    $artifactTwo |
        ForEach-Object { "{0}:{1}" -f $_.Name, $_.Sha256 } |
        Sort-Object
)

$artifactHashesEqual =
    @(
        Compare-Object `
            -ReferenceObject $artifactHashesOne `
            -DifferenceObject $artifactHashesTwo `
            -CaseSensitive
    ).Count -eq 0

$warningLinesOne = @(
    $compileOneResult.Output |
        Where-Object { $_ -match "\bwarning:|\bWarnung:" }
)

$warningLinesTwo = @(
    $compileTwoResult.Output |
        Where-Object { $_ -match "\bwarning:|\bWarnung:" }
)

$summaryLinesOne = @(
    $compileOneResult.Output |
        Where-Object {
            $_ -match "Sketch uses|Global variables use|Der Sketch verwendet|Globale Variablen"
        }
)

$summaryLinesTwo = @(
    $compileTwoResult.Output |
        Where-Object {
            $_ -match "Sketch uses|Global variables use|Der Sketch verwendet|Globale Variablen"
        }
)

$statusAfter = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

Assert-ExactStringSet `
    -Expected $statusBefore `
    -Actual $statusAfter `
    -Description "repository status"

Write-Host ""
Write-Host "ADR-0054 Increment 54B1 library-based compilation"
Write-Host ""
Write-Host "Computer                    :" $env:COMPUTERNAME
Write-Host "Repository unchanged        :" $true
Write-Host "Local secrets present       :" (
    Test-Path -LiteralPath $localSecretsPath -PathType Leaf)
Write-Host "Local secrets read          :" $false
Write-Host "Application .ino/.cpp/.h    :" (
    "{0}/{1}/{2}" -f $stagedInoCount, $stagedCppCount, $stagedHeaderCount)
Write-Host "Framework .cpp/.h           :" "26/27"
Write-Host "Arduino CLI version exact   :" $true
Write-Host "ESP32 core version exact    :" $true
Write-Host "FQBN exact                  :" ($buildFqbn -ceq $fqbn)
Write-Host "PowerShell process PATH only:" $true
Write-Host "Compiler executable         :" $compilerExecutable
Write-Host "Compile 1 succeeded         :" ($compileOneResult.ExitCode -eq 0)
Write-Host "Compile 2 succeeded         :" ($compileTwoResult.ExitCode -eq 0)
Write-Host "Compile 1 warning lines     :" $warningLinesOne.Count
Write-Host "Compile 2 warning lines     :" $warningLinesTwo.Count
Write-Host "Artifact names equal        :" $true
Write-Host "Artifact lengths equal      :" $true
Write-Host "Artifact hashes equal       :" $artifactHashesEqual
Write-Host "Evidence root               :" $EvidenceRoot
Write-Host ""
Write-Host "Compiler version:"
$compilerVersion | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Compile 1 size summary:"
$summaryLinesOne | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Compile 2 size summary:"
$summaryLinesTwo | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Compile 1 artifacts:"
$artifactOne | Format-Table -AutoSize
Write-Host "Compile 2 artifacts:"
$artifactTwo | Format-Table -AutoSize
Write-Host "No install, update, upload, board detection, or serial-port command was invoked."
Write-Host "The evidence directory is retained outside the repository."
