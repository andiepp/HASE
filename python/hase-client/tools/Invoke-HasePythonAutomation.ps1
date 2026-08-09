[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProfilePath,

    [ValidateSet("Health", "Kel103SameValuePropertyWrite")]
    [string] $Workflow = "Health",

    [switch] $ConfirmSameValueWrite
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
if ($Workflow -eq "Kel103SameValuePropertyWrite" `
    -and -not $ConfirmSameValueWrite.IsPresent)
{
    Write-Error "HASE automation failed: same-value-write-confirmation-required."
    exit 1
}
if ($Workflow -eq "Health" -and $ConfirmSameValueWrite.IsPresent)
{
    Write-Error "HASE automation failed: confirmation-not-applicable."
    exit 1
}
if (-not (Test-AbsolutePath -Path $ProfilePath) `
    -or -not (Test-Path -LiteralPath $ProfilePath -PathType Leaf))
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
        & $automationPython -m hase._automation_health $ProfilePath
    }
    else
    {
        & $automationPython `
            -m hase._automation_same_value_property_write `
            $ProfilePath `
            "confirm-same-value-write"
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
