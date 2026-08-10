[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $DesktopProfilePath,
    [Parameter(Mandatory = $true)] [string] $AutomationInstallationDirectory,
    [Parameter(Mandatory = $true)] [string] $MiniPcCredentialDirectory,
    [Parameter(Mandatory = $true)] [string] $MiniPcCertificatePath,
    [Parameter(Mandatory = $true)] [string] $MiniPcPrivateKeyPath,
    [Parameter(Mandatory = $true)] [string] $MiniPcProfilePath,
    [Parameter(Mandatory = $true)] [string] $TargetRegistryPath,
    [Parameter(Mandatory = $true)] [string] $TransferArchivePath,
    [Parameter(Mandatory = $true)] [string] $RollbackDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-AbsolutePath([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]')) { throw "path" }
    return [System.IO.Path]::GetFullPath($Value)
}

function Test-Within([string] $Parent, [string] $Candidate)
{
    $prefix = [System.IO.Path]::GetFullPath($Parent).TrimEnd("\") + "\"
    return [System.IO.Path]::GetFullPath($Candidate).StartsWith(
        $prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePoint([string] $Path)
{
    $current = if (Test-Path -LiteralPath $Path) { $Path } else { Split-Path -Parent $Path }
    while (-not [string]::IsNullOrWhiteSpace($current))
    {
        if (Test-Path -LiteralPath $current)
        {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
            { throw "reparse" }
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

try
{
    if ($env:OS -ne "Windows_NT") { throw "platform" }
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $packageDirectory "..\.."))
    $python = Join-Path $packageDirectory ".venv\Scripts\python.exe"
    if (-not (Test-Path -LiteralPath $python -PathType Leaf)) { throw "python" }
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0) { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $desktopProfile = Resolve-AbsolutePath $DesktopProfilePath
    if (-not (Test-Path -LiteralPath $desktopProfile -PathType Leaf)) { throw "desktop-profile" }
    Assert-NoReparsePoint $desktopProfile
    & $python -c "from hase import load_runtime_host_profile; import sys; load_runtime_host_profile(sys.argv[1])" $desktopProfile
    if ($LASTEXITCODE -ne 0) { throw "desktop-profile" }

    $installation = Resolve-AbsolutePath $AutomationInstallationDirectory
    $credentialDirectory = Resolve-AbsolutePath $MiniPcCredentialDirectory
    $certificate = Resolve-AbsolutePath $MiniPcCertificatePath
    $privateKey = Resolve-AbsolutePath $MiniPcPrivateKeyPath
    $profile = Resolve-AbsolutePath $MiniPcProfilePath
    $registry = Resolve-AbsolutePath $TargetRegistryPath
    $transfer = Resolve-AbsolutePath $TransferArchivePath
    $rollback = Resolve-AbsolutePath $RollbackDirectory
    $outputs = @($installation, $credentialDirectory, $certificate, $privateKey, $profile, $registry, $transfer, $rollback)
    if (@($outputs | Sort-Object -Unique).Count -ne $outputs.Count) { throw "outputs" }
    foreach ($output in $outputs)
    {
        if (Test-Path -LiteralPath $output) { throw "outputs" }
        if (Test-Within $repositoryRoot $output) { throw "outputs" }
    }
    foreach ($leaf in @($certificate, $privateKey, $profile))
    {
        if (-not (Test-Within $credentialDirectory $leaf)) { throw "credential-custody" }
    }
    foreach ($external in @($installation, $credentialDirectory, $registry, $transfer, $rollback))
    {
        $parent = Split-Path -Parent $external
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "parent" }
        Assert-NoReparsePoint $parent
    }
    if ((Test-Within $credentialDirectory $registry) `
        -or (Test-Within $credentialDirectory $installation) `
        -or (Test-Within $credentialDirectory $rollback) `
        -or (Test-Within $installation $registry) `
        -or (Test-Within $installation $credentialDirectory))
    { throw "external-custody" }
    if ([string]::Equals($desktopProfile, $profile, [System.StringComparison]::OrdinalIgnoreCase))
    { throw "profile-sharing" }

    Write-Host "Repository baseline ready       : True"
    Write-Host "Runtime processes stopped       : True"
    Write-Host "Desktop Python profile ready    : True"
    Write-Host "MiniPC credential custody absent: True"
    Write-Host "Target registry absent          : True"
    Write-Host "Automation installation absent  : True"
    Write-Host "Transfer target absent          : True"
    Write-Host "External rollback target absent : True"
    Write-Host "Laptop MiniPC credential ready  : True"
}
catch
{
    Write-Error "Laptop MiniPC Python credential readiness failed."
    exit 1
}
