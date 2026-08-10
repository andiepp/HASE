[CmdletBinding()]
param([Parameter(Mandatory = $true)] [string] $RollbackDirectory)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

try
{
    if ($env:COMPUTERNAME -cne "LTAEP") { throw "machine" }
    $rollback = [IO.Path]::GetFullPath($RollbackDirectory)
    $planPath = Join-Path $rollback "import-plan.json"
    if (-not (Test-Path -LiteralPath $planPath -PathType Leaf)) { throw "plan" }
    $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
    if ($plan.schemaVersion -ne 1 `
        -or $plan.purpose -cne "hase-laptop-minipc-python-credential-import")
    { throw "plan" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }
    if (-not (Test-Path -LiteralPath $plan.desktopProfilePath -PathType Leaf) `
        -or (Get-FileHash -LiteralPath $plan.desktopProfilePath -Algorithm SHA256).Hash.ToLowerInvariant() `
            -cne [string]$plan.desktopProfileSha256 `
        -or (Get-Acl -LiteralPath $plan.desktopProfilePath).Sddl `
            -cne [string]$plan.desktopProfileSddl)
    { throw "desktop-profile" }
    if (Test-Path -LiteralPath $plan.targetRegistryPath)
    { Remove-Item -LiteralPath $plan.targetRegistryPath -Force }
    if (Test-Path -LiteralPath $plan.credentialDirectory)
    { Remove-Item -LiteralPath $plan.credentialDirectory -Recurse -Force }
    $stage = Join-Path $rollback "credential.stage"
    if (Test-Path -LiteralPath $stage)
    { Remove-Item -LiteralPath $stage -Recurse -Force }
    $journal = Join-Path $rollback "import-journal.json"
    if (Test-Path -LiteralPath $journal) { Remove-Item -LiteralPath $journal -Force }
    Write-Host "Desktop profile unchanged    : True"
    Write-Host "MiniPC credential absent     : True"
    Write-Host "Target registry absent       : True"
    Write-Host "Rollback evidence retained   : True"
    Write-Host "Laptop import recovery complete: True"
}
catch
{
    Write-Error "Laptop MiniPC Python credential import recovery failed."
    exit 1
}
