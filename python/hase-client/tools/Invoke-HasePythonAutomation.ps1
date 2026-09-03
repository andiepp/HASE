[CmdletBinding()]
param(
    [string] $ProfilePath,

    [string] $TargetRegistryPath,

    [ValidateSet("desktop-runtime-host", "minipc-runtime-host")]
    [string] $TargetId,

    [ValidateSet(
        "Health",
        "MiniPcAuthoritativePropertyRead")]
    [string] $Workflow = "Health"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-AbsolutePath
{
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path))
    {
        return $false
    }
    try
    {
        return [System.IO.Path]::IsPathRooted($Path) `
            -and [string]::Equals(
                [System.IO.Path]::GetFullPath($Path),
                $Path,
                [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch
    {
        return $false
    }
}

$automationPython = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"
$manifestPath = Join-Path $PSScriptRoot "installation-manifest.json"

if (-not (Test-Path -LiteralPath $automationPython -PathType Leaf) `
    -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf))
{
    Write-Error "HASE automation failed: installation-invalid."
    exit 1
}
$profileSupplied = -not [string]::IsNullOrWhiteSpace($ProfilePath)
$registrySupplied = -not [string]::IsNullOrWhiteSpace($TargetRegistryPath)
$targetSupplied = -not [string]::IsNullOrWhiteSpace($TargetId)
if (($profileSupplied -and ($registrySupplied -or $targetSupplied)) `
    -or (-not $profileSupplied -and (-not $registrySupplied -or -not $targetSupplied)))
{
    Write-Error "HASE automation failed: target-selection-invalid."
    exit 1
}

$selectedProfilePath = $ProfilePath
if ($registrySupplied)
{
    if (-not (Test-AbsolutePath -Path $TargetRegistryPath) `
        -or -not (Test-Path -LiteralPath $TargetRegistryPath -PathType Leaf))
    {
        Write-Error "HASE automation failed: target-registry-path-invalid."
        exit 1
    }
    $selectionPythonPath = $env:PYTHONPATH
    try
    {
        Push-Location $PSScriptRoot
        $env:PYTHONPATH = $null
        $selectionOutput = @(
            & $automationPython `
                -m hase._automation_target_selection `
                $TargetRegistryPath `
                $TargetId `
                $PSScriptRoot
        )
    }
    finally
    {
        $env:PYTHONPATH = $selectionPythonPath
        Pop-Location
    }
    if ($LASTEXITCODE -ne 0 -or $selectionOutput.Count -ne 1)
    {
        exit 1
    }
    $selectedProfilePath = [string] $selectionOutput[0]
}
if (-not (Test-AbsolutePath -Path $selectedProfilePath) `
    -or -not (Test-Path -LiteralPath $selectedProfilePath -PathType Leaf))
{
    Write-Error "HASE automation failed: profile-path-invalid."
    exit 1
}

$locationPushed = $false
$previousPythonPath = $env:PYTHONPATH
try
{
    Push-Location $PSScriptRoot
    $locationPushed = $true
    $env:PYTHONPATH = $null
    if ($Workflow -eq "Health")
    {
        & $automationPython -m hase._automation_health $selectedProfilePath
    }
    else
    {
        & $automationPython `
            -m hase._automation_minipc_authoritative_property_read `
            $selectedProfilePath
    }
    if ($LASTEXITCODE -ne 0)
    {
        exit 1
    }
}
catch
{
    Write-Error "HASE automation failed: unexpected-failure."
    exit 1
}
finally
{
    $env:PYTHONPATH = $previousPythonPath
    if ($locationPushed)
    {
        Pop-Location
    }
}
