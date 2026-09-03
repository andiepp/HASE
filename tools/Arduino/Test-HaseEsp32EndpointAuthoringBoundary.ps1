param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expectedCommit =
    "afc427a633af8ff752887820761e85fc16430402"

$expectedChangedPaths = @(
    "HaseESP32/EndpointApplication.cpp",
    "HaseESP32/EndpointApplication.h",
    "HaseESP32/EndpointConfiguration.h",
    "HaseESP32/EndpointDefinition.cpp",
    "HaseESP32/HaseBme280Sensor.cpp",
    "HaseESP32/HaseBme280Sensor.h",
    "HaseESP32/HaseESP32.ino",
    "HaseESP32/HasePhysicalEndpointDefinition.cpp",
    "HaseESP32/HasePhysicalEndpointDefinition.h",
    "HaseESP32/HasePhysicalEndpointDescriptor.cpp",
    "HaseESP32/HasePhysicalEndpointDescriptor.h",
    "HaseESP32/HasePhysicalEventPublisher.cpp",
    "HaseESP32/HasePhysicalEventPublisher.h",
    "HaseESP32/HasePhysicalPropertyService.cpp",
    "HaseESP32/HasePhysicalPropertyService.h",
    "HaseESP32/HasePushButton.cpp",
    "HaseESP32/HasePushButton.h",
    "HaseESP32/HaseSecrets.example.h",
    "HaseESP32/HaseStatusLed.cpp",
    "HaseESP32/HaseStatusLed.h",
    "libraries/HaseEsp32Endpoint/src/HaseEndpointApplication.h",
    "libraries/HaseEsp32Endpoint/src/HaseEndpointConfiguration.h",
    "libraries/HaseEsp32Endpoint/src/HaseEndpointRuntime.cpp",
    "libraries/HaseEsp32Endpoint/src/HaseEndpointRuntime.h",
    "libraries/HaseEsp32Endpoint/src/HaseEsp32Endpoint.h",
    "templates/HaseESP32/HaseSecrets.example.h",
    "tests/Arduino/HaseEndpointRuntimeValidation/HaseEndpointRuntimeValidation.ino",
    "tools/Arduino/Test-HaseEsp32EndpointApplicationBoundary.ps1",
    "tools/Arduino/Test-HaseEsp32EndpointAuthoringBoundary.ps1",
    "tools/Arduino/Test-HaseEsp32EndpointCompilation.ps1"
) | Sort-Object

$expectedApplicationFiles = @(
    "EndpointApplication.cpp",
    "EndpointApplication.h",
    "EndpointConfiguration.h",
    "EndpointDefinition.cpp",
    "HaseESP32.ino"
) | Sort-Object

$obsoleteApplicationFiles = @(
    "HaseBme280Sensor.cpp",
    "HaseBme280Sensor.h",
    "HasePhysicalEndpointDefinition.cpp",
    "HasePhysicalEndpointDefinition.h",
    "HasePhysicalEndpointDescriptor.cpp",
    "HasePhysicalEndpointDescriptor.h",
    "HasePhysicalEventPublisher.cpp",
    "HasePhysicalEventPublisher.h",
    "HasePhysicalPropertyService.cpp",
    "HasePhysicalPropertyService.h",
    "HasePushButton.cpp",
    "HasePushButton.h",
    "HaseSecrets.example.h",
    "HaseStatusLed.cpp",
    "HaseStatusLed.h"
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

function Assert-ContainsExactly
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [int]$Count,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $actualCount =
        [regex]::Matches(
            $Text,
            $Pattern,
            [System.Text.RegularExpressions.RegexOptions]::Multiline).
            Count

    if ($actualCount -ne $Count)
    {
        $message = "The {0} count is invalid." -f $Description
        throw $message
    }
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container))
{
    throw "The repository root does not exist."
}

& git -C $RepositoryRoot fetch origin main

if ($LASTEXITCODE -ne 0)
{
    throw "git fetch origin main failed."
}

$head = @(Invoke-GitLines -Arguments @("rev-parse", "HEAD"))
$origin = @(Invoke-GitLines -Arguments @("rev-parse", "origin/main"))
$branch = @(Invoke-GitLines -Arguments @("branch", "--show-current"))

if ($head.Count -ne 1 -or $head[0].Trim() -cne $expectedCommit)
{
    throw "Repository HEAD is not the approved 54C3 baseline."
}

if ($origin.Count -ne 1 -or $origin[0].Trim() -cne $expectedCommit)
{
    throw "origin/main is not the approved 54C3 baseline."
}

if ($branch.Count -ne 1 -or $branch[0].Trim() -cne "main")
{
    throw "The repository is not on main."
}

$status = @(
    Invoke-GitLines `
        -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
)

$changedPaths = @(
    $status |
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
    -Description "54D1 changed path"

$stagedPaths = @(Invoke-GitLines -Arguments @("diff", "--cached", "--name-only"))

if ($stagedPaths.Count -ne 0)
{
    throw "A 54D1 file was unexpectedly staged."
}

$applicationRoot = Join-Path $RepositoryRoot "HaseESP32"
$actualApplicationFiles = @(
    Get-ChildItem -LiteralPath $applicationRoot -File |
    Where-Object {
        $_.Extension -in @(".ino", ".cpp", ".h") `
            -and $_.Name -cne "HaseSecrets.h"
    } |
    ForEach-Object { $_.Name }
)

Assert-ExactStringSet `
    -Expected $expectedApplicationFiles `
    -Actual $actualApplicationFiles `
    -Description "visible application source"

foreach ($obsoleteFile in $obsoleteApplicationFiles)
{
    if (Test-Path -LiteralPath (Join-Path $applicationRoot $obsoleteFile))
    {
        $message = "An obsolete application file remains: {0}" -f $obsoleteFile
        throw $message
    }
}

$localSecretsPath = Join-Path $applicationRoot "HaseSecrets.h"

& git -C $RepositoryRoot check-ignore --quiet -- "HaseESP32/HaseSecrets.h"

if ($LASTEXITCODE -ne 0)
{
    throw "The local HaseSecrets.h path is not ignored."
}

$trackedSecrets = @(
    Invoke-GitLines -Arguments @("ls-files", "--", "HaseESP32/HaseSecrets.h")
)

if ($trackedSecrets.Count -ne 0)
{
    throw "The local HaseSecrets.h file is unexpectedly tracked."
}

$templatePath =
    Join-Path $RepositoryRoot "templates\HaseESP32\HaseSecrets.example.h"

if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf))
{
    throw "The external secrets template is missing."
}

$inoPath = Join-Path $applicationRoot "HaseESP32.ino"
$configurationPath = Join-Path $applicationRoot "EndpointConfiguration.h"
$definitionPath = Join-Path $applicationRoot "EndpointDefinition.cpp"
$applicationHeaderPath = Join-Path $applicationRoot "EndpointApplication.h"
$applicationSourcePath = Join-Path $applicationRoot "EndpointApplication.cpp"
$runtimeHeaderPath =
    Join-Path $RepositoryRoot "libraries\HaseEsp32Endpoint\src\HaseEndpointRuntime.h"
$runtimeSourcePath =
    Join-Path $RepositoryRoot "libraries\HaseEsp32Endpoint\src\HaseEndpointRuntime.cpp"

$inoLines = @(Get-Content -LiteralPath $inoPath)
$inoText = Get-Content -LiteralPath $inoPath -Raw
$configurationText = Get-Content -LiteralPath $configurationPath -Raw
$definitionText = Get-Content -LiteralPath $definitionPath -Raw
$applicationHeaderText = Get-Content -LiteralPath $applicationHeaderPath -Raw
$applicationSourceText = Get-Content -LiteralPath $applicationSourcePath -Raw
$runtimeHeaderText = Get-Content -LiteralPath $runtimeHeaderPath -Raw
$runtimeSourceText = Get-Content -LiteralPath $runtimeSourcePath -Raw

if ($inoLines.Count -gt 60)
{
    throw "HaseESP32.ino exceeds the approved minimal-authoring boundary."
}

foreach ($forbiddenPattern in @(
    "WiFi\.",
    "HaseTcpTransport",
    "HaseMdnsAdvertiser",
    "HaseUtcClock",
    "HaseProtocolEnvelope",
    "HaseProtocolDispatcher",
    "HaseEndpointRequestProcessor",
    "HaseEventNotificationHandler",
    "Adafruit",
    "pinMode",
    "digitalRead",
    "digitalWrite"))
{
    if ($inoText -match $forbiddenPattern)
    {
        throw "HaseESP32.ino contains framework or hardware implementation."
    }
}

foreach ($requiredConfigurationPattern in @(
    "5000",
    '"doit-esp32-devkitc-v4-01"',
    "4096",
    "5000",
    "15000"))
{
    if ($configurationText -notmatch $requiredConfigurationPattern)
    {
        throw "EndpointConfiguration.h is missing an approved value."
    }
}

foreach ($requiredIdentity in @(
    '"doit-esp32-devkitc-v4-01"',
    '"environment-sensor-01"',
    '"controller-01"',
    '"Environment.Temperature"',
    '"Environment.RelativeHumidity"',
    '"Environment.AirPressure"',
    '"Controller.StatusLedEnabled"',
    '"Controller.ToggleStatusLed"',
    '"Controller.ButtonPressed"'))
{
    if ($definitionText -notmatch [regex]::Escape($requiredIdentity))
    {
        throw "EndpointDefinition.cpp is missing an approved identity."
    }
}

Assert-ContainsExactly `
    -Text $definitionText `
    -Pattern "properties\[[0-3]\]\s*=" `
    -Count 4 `
    -Description "Property registration"

Assert-ContainsExactly `
    -Text $definitionText `
    -Pattern "commands\[0\]\s*=" `
    -Count 1 `
    -Description "Command registration"

Assert-ContainsExactly `
    -Text $definitionText `
    -Pattern "events\[0\]\s*=" `
    -Count 1 `
    -Description "Event registration"

$applicationText = $applicationHeaderText + "`n" + $applicationSourceText

foreach ($requiredHardwarePattern in @(
    "Bme280SdaPin\s*=\s*21",
    "Bme280SclPin\s*=\s*22",
    "Bme280I2cAddress\s*=\s*0x76",
    "StatusLedPin\s*=\s*16",
    "ButtonPin\s*=\s*17",
    "ButtonDebounceMilliseconds\s*=\s*50",
    "INPUT_PULLUP",
    "Adafruit_BME280",
    "publishNullEvent"))
{
    if ($applicationText -notmatch $requiredHardwarePattern)
    {
        throw "EndpointApplication is missing approved physical behavior."
    }
}

Assert-ContainsExactly `
    -Text $applicationSourceText `
    -Pattern "runtime\.publishNullEvent" `
    -Count 1 `
    -Description "application Event publication"

$librarySourceRoot =
    Join-Path $RepositoryRoot "libraries\HaseEsp32Endpoint\src"

$libraryText = @(
    Get-ChildItem -LiteralPath $librarySourceRoot -File |
    Where-Object { $_.Extension -in @(".cpp", ".h") } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
) -join "`n"

foreach ($forbiddenLibraryPattern in @(
    "Adafruit",
    "BME280",
    "GPIO16",
    "GPIO17",
    "StatusLedPin",
    "ButtonPin"))
{
    if ($libraryText -match $forbiddenLibraryPattern)
    {
        throw "The framework library contains endpoint-specific hardware data."
    }
}

Assert-ContainsExactly `
    -Text $runtimeSourceText `
    -Pattern "HaseEndpointDefinitionValidator::Validate" `
    -Count 1 `
    -Description "runtime definition validation"

Assert-ContainsExactly `
    -Text $runtimeSourceText `
    -Pattern "HaseEndpointRequestProcessor::Process" `
    -Count 1 `
    -Description "runtime request processing"

Assert-ContainsExactly `
    -Text $runtimeSourceText `
    -Pattern "HaseEventNotificationHandler::PublishNull" `
    -Count 1 `
    -Description "runtime Event framing"

$validationIndex =
    $runtimeSourceText.IndexOf("HaseEndpointDefinitionValidator::Validate")
$hardwareIndex =
    $runtimeSourceText.IndexOf("_application.beginHardware()")
$wifiIndex =
    $runtimeSourceText.IndexOf("connectToWifi(")
$utcIndex =
    $runtimeSourceText.IndexOf("synchronizeUtcClock()")
$eventIndex =
    $runtimeSourceText.IndexOf("_application.beginEventDetection()")
$networkIndex =
    $runtimeSourceText.IndexOf("startNetworkEndpoint()")

if ($validationIndex -lt 0 `
    -or $hardwareIndex -le $validationIndex `
    -or $wifiIndex -le $hardwareIndex `
    -or $utcIndex -le $wifiIndex `
    -or $eventIndex -le $utcIndex `
    -or $networkIndex -le $eventIndex)
{
    throw "The runtime startup ordering contract is invalid."
}

foreach ($requiredRuntimeToken in @(
    "class HaseEndpointRuntime",
    "bool begin(",
    "void update()",
    "bool publishNullEvent(",
    "BufferCapacity"))
{
    if (($runtimeHeaderText + $runtimeSourceText) -notmatch
        [regex]::Escape($requiredRuntimeToken))
    {
        throw "The public runtime facade is incomplete."
    }
}

& git -C $RepositoryRoot diff --check

if ($LASTEXITCODE -ne 0)
{
    throw "git diff --check failed."
}

Write-Host ""
Write-Host "ADR-0054 Increment 54D1 focused authoring-boundary validation"
Write-Host ""
Write-Host "Computer                    :" $env:COMPUTERNAME
Write-Host "HEAD exact                  :" ($head[0].Trim() -ceq $expectedCommit)
Write-Host "origin/main exact           :" ($origin[0].Trim() -ceq $expectedCommit)
Write-Host "Changed paths exact         :" $true
Write-Host "Changed path count          :" $changedPaths.Count
Write-Host "Files staged                :" $false
Write-Host "Visible tracked source tabs :" $actualApplicationFiles.Count
Write-Host "Local secrets ignored       :" $true
Write-Host "Local secrets read          :" $false
Write-Host "External secrets template   :" $true
Write-Host "Application registrations   :" "4 properties / 1 command / 1 event"
Write-Host "Application Event calls     :" 1
Write-Host "Library hardware references :" 0
Write-Host "Runtime validation calls    :" 1
Write-Host "Runtime request calls       :" 1
Write-Host "Runtime Event framing calls :" 1
Write-Host "Repository diff check       :" $true
Write-Host ""
Write-Host "No repository or evidence file was changed."
Write-Host "No compilation, upload, deployment, serial access, or physical mutation occurred."
Write-Host "No local Wi-Fi secret was read or hashed."
