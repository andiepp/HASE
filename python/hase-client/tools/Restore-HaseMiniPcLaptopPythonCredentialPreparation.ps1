[CmdletBinding()]
param([Parameter(Mandatory = $true)] [string] $RollbackDirectory)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

try
{
    $rollback = [IO.Path]::GetFullPath($RollbackDirectory)
    $planPath = Join-Path $rollback "transaction-plan.json"
    if (-not (Test-Path -LiteralPath $planPath -PathType Leaf)) { throw "plan" }
    $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
    if ($plan.schemaVersion -ne 1 `
        -or $plan.purpose -cne "hase-minipc-laptop-python-credential-transaction")
    { throw "plan" }
    foreach ($entry in @($plan.entries))
    {
        if ($entry.existed)
        {
            if (-not (Test-Path -LiteralPath $entry.path -PathType Leaf) `
                -or (Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash.ToLowerInvariant() `
                    -cne [string]$entry.sha256)
            { throw "state" }
        }
        elseif (Test-Path -LiteralPath $entry.path) { throw "publication-state" }
    }
    $template = [string]$plan.profileTemplatePath
    if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { throw "template" }
    Remove-Item -LiteralPath $template -Force
    Remove-Item -LiteralPath $rollback -Recurse -Force
    Write-Host "Publication state unchanged : True"
    Write-Host "Profile template removed    : True"
    Write-Host "Rollback evidence removed   : True"
    Write-Host "Preparation recovery complete: True"
}
catch
{
    Write-Error "MiniPC Laptop Python credential preparation recovery failed."
    exit 1
}
