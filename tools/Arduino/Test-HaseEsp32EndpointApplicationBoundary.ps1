param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expectedCommit =
    "7e4042069e154183d80cf82682e43e9319c7e9ff"

$expectedChangedPaths = @(
    "HaseEndpoint/HaseEndpoint.ino",
    "HaseEndpoint/HasePhysicalEndpointDefinition.cpp",
    "HaseEndpoint/HasePhysicalEndpointDefinition.h",
    "HaseEndpoint/HasePhysicalEventPublisher.cpp",
    "HaseEndpoint/HasePhysicalEventPublisher.h",
    "HaseEndpoint/HasePhysicalExecuteCommandHandler.cpp",
    "HaseEndpoint/HasePhysicalExecuteCommandHandler.h",
    "HaseEndpoint/HasePhysicalPropertyService.cpp",
    "HaseEndpoint/HasePhysicalPropertyService.h",
    "HaseEndpoint/HasePhysicalReadPropertyHandler.cpp",
    "HaseEndpoint/HasePhysicalReadPropertyHandler.h",
    "HaseEndpoint/HasePhysicalWritePropertyHandler.cpp",
    "HaseEndpoint/HasePhysicalWritePropertyHandler.h",
    "libraries/HaseEsp32Endpoint/src/HaseDiscoverHandler.cpp",
    "libraries/HaseEsp32Endpoint/src/HaseDiscoverHandler.h",
    "libraries/HaseEsp32Endpoint/src/HaseEsp32Endpoint.h",
    "libraries/HaseEsp32Endpoint/src/HaseEventNotificationHandler.cpp",
    "libraries/HaseEsp32Endpoint/src/HaseEventNotificationHandler.h",
    "tools/Arduino/Test-HaseEsp32EndpointApplicationBoundary.ps1",
    "tools/Arduino/Test-HaseEsp32EndpointCompilation.ps1"
) | Sort-Object

$obsoletePaths = @(
    "HaseEndpoint/HasePhysicalExecuteCommandHandler.cpp",
    "HaseEndpoint/HasePhysicalExecuteCommandHandler.h",
    "HaseEndpoint/HasePhysicalReadPropertyHandler.cpp",
    "HaseEndpoint/HasePhysicalReadPropertyHandler.h",
    "HaseEndpoint/HasePhysicalWritePropertyHandler.cpp",
    "HaseEndpoint/HasePhysicalWritePropertyHandler.h",
    "libraries/HaseEsp32Endpoint/src/HaseDiscoverHandler.cpp",
    "libraries/HaseEsp32Endpoint/src/HaseDiscoverHandler.h"
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

function Get-LiteralCount
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    return [regex]::Matches(
        $Text,
        [regex]::Escape($Token)).Count
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container))
{
    throw "The repository root does not exist."
}

$head = @(Invoke-GitLines -Arguments @("rev-parse", "HEAD"))
$origin = @(Invoke-GitLines -Arguments @("rev-parse", "origin/main"))
$branch = @(Invoke-GitLines -Arguments @("branch", "--show-current"))

if ($head.Count -ne 1 -or $head[0].Trim() -cne $expectedCommit)
{
    throw "Repository HEAD is not the approved 54C2 baseline."
}

if ($origin.Count -ne 1 -or $origin[0].Trim() -cne $expectedCommit)
{
    throw "origin/main is not the approved 54C2 baseline."
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
    -Description "54C3 changed path"

$stagedPaths = @(
    Invoke-GitLines -Arguments @("diff", "--cached", "--name-only")
)

if ($stagedPaths.Count -ne 0)
{
    throw "54C3 files must not be staged during validation."
}

foreach ($relativePath in $obsoletePaths)
{
    $absolutePath = Join-Path $RepositoryRoot ($relativePath.Replace('/', '\'))

    if (Test-Path -LiteralPath $absolutePath)
    {
        $message = "An obsolete source remains: {0}" -f $relativePath
        throw $message
    }
}

$sketchPath = Join-Path $RepositoryRoot "HaseEndpoint\HaseEndpoint.ino"
$definitionPath = Join-Path $RepositoryRoot "HaseEndpoint\HasePhysicalEndpointDefinition.cpp"
$servicePath = Join-Path $RepositoryRoot "HaseEndpoint\HasePhysicalPropertyService.cpp"
$physicalEventPath = Join-Path $RepositoryRoot "HaseEndpoint\HasePhysicalEventPublisher.cpp"
$genericEventPath = Join-Path $RepositoryRoot "libraries\HaseEsp32Endpoint\src\HaseEventNotificationHandler.cpp"
$librarySourceRoot = Join-Path $RepositoryRoot "libraries\HaseEsp32Endpoint\src"

$sketchSource = [System.IO.File]::ReadAllText($sketchPath)
$definitionSource = [System.IO.File]::ReadAllText($definitionPath)
$serviceSource = [System.IO.File]::ReadAllText($servicePath)
$physicalEventSource = [System.IO.File]::ReadAllText($physicalEventPath)
$genericEventSource = [System.IO.File]::ReadAllText($genericEventPath)

if ((Get-LiteralCount -Text $sketchSource -Token "HaseEndpointRequestProcessor::Process(") -ne 1)
{
    throw "The sketch request-processor invocation count is invalid."
}

$validationIndex = $sketchSource.IndexOf(
    "HaseEndpointDefinitionValidator::Validate(",
    [System.StringComparison]::Ordinal)

$hardwareIndex = $sketchSource.IndexOf(
    "initializeEnvironmentSensor()",
    $sketchSource.IndexOf("void setup()", [System.StringComparison]::Ordinal),
    [System.StringComparison]::Ordinal)

if ($validationIndex -lt 0 -or $hardwareIndex -lt 0 -or $validationIndex -ge $hardwareIndex)
{
    throw "Endpoint definition validation does not precede hardware startup."
}

foreach ($token in @(
    "readTemperature",
    "readRelativeHumidity",
    "readAirPressure",
    "readStatusLedEnabled",
    "writeStatusLedEnabled",
    "toggleStatusLed"))
{
    if ((Get-LiteralCount -Text $definitionSource -Token $token) -ne 3)
    {
        $message = "A physical callback binding count is invalid: {0}" -f $token
        throw $message
    }
}

foreach ($forbiddenIdentity in @(
    "environment-sensor-01",
    "controller-01",
    "physical.environment-sensor.temperature",
    "physical.environment-sensor.relative-humidity",
    "physical.environment-sensor.air-pressure",
    "physical.controller.status-led-enabled",
    "Controller.ToggleStatusLed"))
{
    if ($serviceSource.IndexOf(
            $forbiddenIdentity,
            [System.StringComparison]::Ordinal) -ge 0)
    {
        throw "The physical service still routes by descriptor identity."
    }
}

if ($serviceSource.IndexOf("strcmp", [System.StringComparison]::Ordinal) -ge 0)
{
    throw "The physical service still performs string routing."
}

if ((Get-LiteralCount -Text $physicalEventSource -Token "HaseEventNotificationHandler::PublishNull(") -ne 1)
{
    throw "The physical Event publisher does not use the generic boundary exactly once."
}

foreach ($forbiddenPhysicalEventToken in @(
    "HaseBinaryProtocolWriter",
    "HaseProtocolEnvelope",
    "writeInt64",
    "writeByte",
    "EventNotificationMessageType"))
{
    if ($physicalEventSource.IndexOf(
            $forbiddenPhysicalEventToken,
            [System.StringComparison]::Ordinal) -ge 0)
    {
        throw "The physical Event detector still owns protocol framing."
    }
}

foreach ($requiredGenericEventToken in @(
    "registration.instrument->id",
    "registration.event->path",
    "utcClock.tryGetUnixTimeMilliseconds(",
    "transport.writeFrame("))
{
    if ($genericEventSource.IndexOf(
            $requiredGenericEventToken,
            [System.StringComparison]::Ordinal) -lt 0)
    {
        $message = "The generic Event boundary is missing: {0}" -f $requiredGenericEventToken
        throw $message
    }
}

$librarySources = @(
    Get-ChildItem -LiteralPath $librarySourceRoot -File |
    Where-Object { $_.Extension -in @(".cpp", ".h") } |
    ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
) -join "`n"

foreach ($endpointSpecificToken in @(
    "doit-esp32-devkitc-v4-01",
    "environment-sensor-01",
    "controller-01",
    "GPIO16",
    "GPIO17",
    "BME280",
    "Adafruit_"))
{
    if ($librarySources.IndexOf(
            $endpointSpecificToken,
            [System.StringComparison]::Ordinal) -ge 0)
    {
        throw "The generic library contains endpoint-specific application knowledge."
    }
}

foreach ($obsoleteType in @(
    "HaseDiscoverHandler",
    "HasePhysicalReadPropertyHandler",
    "HasePhysicalWritePropertyHandler",
    "HasePhysicalExecuteCommandHandler"))
{
    if ($sketchSource.IndexOf(
            $obsoleteType,
            [System.StringComparison]::Ordinal) -ge 0)
    {
        throw "The sketch still references an obsolete handler."
    }
}

& git -C $RepositoryRoot diff --check

if ($LASTEXITCODE -ne 0)
{
    throw "git diff --check failed."
}

Write-Host ""
Write-Host "ADR-0054 Increment 54C3 focused application-boundary validation"
Write-Host ""
Write-Host "Computer                    :" $env:COMPUTERNAME
Write-Host "HEAD exact                  :" ($head[0].Trim() -ceq $expectedCommit)
Write-Host "origin/main exact           :" ($origin[0].Trim() -ceq $expectedCommit)
Write-Host "Changed paths exact         :" $true
Write-Host "Changed path count          :" $changedPaths.Count
Write-Host "Files staged                :" $false
Write-Host "Definition validated first  :" $true
Write-Host "Capability callbacks bound  :" "4 properties / 1 command / 1 event"
Write-Host "Request processor calls     :" 1
Write-Host "Physical identity routing   :" 0
Write-Host "Physical Event framing      :" 0
Write-Host "Generic Event boundary      :" $true
Write-Host "Endpoint-specific library data:" 0
Write-Host "Obsolete handlers present   :" 0
Write-Host "Repository diff check       :" $true
Write-Host ""
Write-Host "No repository or evidence file was changed."
Write-Host "No compilation, upload, deployment, serial access, or physical mutation occurred."
Write-Host "No local Wi-Fi secret was read or hashed."
