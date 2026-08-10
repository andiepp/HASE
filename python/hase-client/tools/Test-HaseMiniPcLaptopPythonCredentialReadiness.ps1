[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $MiniPcConfigurationPath,
    [Parameter(Mandatory = $true)] [string] $ApplicationProfilePath,
    [Parameter(Mandatory = $true)] [string] $AuthorityManifestPath,
    [Parameter(Mandatory = $true)] [string] $AuthorityRollbackEvidencePath,
    [Parameter(Mandatory = $true)] [string] $StagingDirectory,
    [Parameter(Mandatory = $true)] [string] $CertificatePath,
    [Parameter(Mandatory = $true)] [string] $PrivateKeyPath,
    [Parameter(Mandatory = $true)] [string] $ProfilePath,
    [Parameter(Mandatory = $true)] [string] $TransferArchivePath,
    [Parameter(Mandatory = $true)] [string] $RollbackDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$laptopPrincipal = "hase-laptop-python-minipc"
$plannedPermissions = @(
    "runtime-host.snapshot.read",
    "property.authoritative.read")

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
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0) { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $configurationPath = Resolve-AbsolutePath $MiniPcConfigurationPath
    $applicationPath = Resolve-AbsolutePath $ApplicationProfilePath
    $manifestPath = Resolve-AbsolutePath $AuthorityManifestPath
    $authorityRollbackPath = Resolve-AbsolutePath $AuthorityRollbackEvidencePath
    foreach ($input in @($configurationPath, $applicationPath, $manifestPath, $authorityRollbackPath))
    {
        if (-not (Test-Path -LiteralPath $input -PathType Leaf)) { throw "input" }
        Assert-NoReparsePoint $input
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $authorityRollback = Get-Content -LiteralPath $authorityRollbackPath -Raw | ConvertFrom-Json
    foreach ($field in @("purpose", "thumbprint", "certificateSha256", "personalStore", "trustedStore"))
    {
        if ([string]$manifest.$field -cne [string]$authorityRollback.$field) { throw "authority" }
    }
    if ($manifest.purpose -cne "hase-minipc-python-client-authority" `
        -or $manifest.personalStore -cne "CurrentUser/My" `
        -or $manifest.trustedStore -cne "CurrentUser/Root") { throw "authority" }
    $personal = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Thumbprint -ieq [string]$manifest.thumbprint })
    $trusted = @(Get-ChildItem Cert:\CurrentUser\Root | Where-Object {
        $_.Thumbprint -ieq [string]$manifest.thumbprint })
    if ($personal.Count -ne 1 -or $trusted.Count -ne 1 `
        -or -not $personal[0].HasPrivateKey `
        -or [Convert]::ToBase64String($personal[0].RawData) -cne `
            [Convert]::ToBase64String($trusted[0].RawData)) { throw "authority" }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $authorityHash = [BitConverter]::ToString($sha.ComputeHash($personal[0].RawData)).Replace("-", "").ToLowerInvariant() }
    finally { $sha.Dispose() }
    if ($authorityHash -cne [string]$manifest.certificateSha256) { throw "authority" }

    $configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
    $application = Get-Content -LiteralPath $applicationPath -Raw | ConvertFrom-Json
    if ((Resolve-AbsolutePath ([string]$application.privateNetworkConfigurationFilePath)) `
        -ne $configurationPath) { throw "active-state" }
    $enrollmentPath = Resolve-AbsolutePath ([string]$configuration.clientEnrollmentFilePath)
    $authorizationPath = Resolve-AbsolutePath ([string]$application.authorizationPolicyFilePath)
    foreach ($input in @($enrollmentPath, $authorizationPath))
    {
        if (-not (Test-Path -LiteralPath $input -PathType Leaf)) { throw "active-state" }
        Assert-NoReparsePoint $input
    }
    $enrollment = Get-Content -LiteralPath $enrollmentPath -Raw | ConvertFrom-Json
    $authorization = Get-Content -LiteralPath $authorizationPath -Raw | ConvertFrom-Json
    if (@($enrollment.enrollments | Where-Object { $_.principalId -eq $laptopPrincipal }).Count -ne 0 `
        -or @($authorization.grants | Where-Object { $_.principalId -eq $laptopPrincipal }).Count -ne 0)
    { throw "laptop-principal-present" }
    $localEnrollments = @($enrollment.enrollments | Where-Object { $_.principalId -eq "hase-python-automation" })
    $localGrants = @($authorization.grants | Where-Object { $_.principalId -eq "hase-python-automation" })
    $localPermissions = @($localGrants.permission | Sort-Object -Unique)
    if ($localEnrollments.Count -ne 1 -or $localGrants.Count -ne 2 `
        -or $localPermissions.Count -ne 2 `
        -or $localPermissions -notcontains "runtime-host.snapshot.read" `
        -or $localPermissions -notcontains "property.authoritative.read")
    { throw "local-python-state" }
    if ($plannedPermissions.Count -ne 2 `
        -or $plannedPermissions -contains "diagnostics.subscribe")
    { throw "planned-grants" }
    $clientPermissions = @(
        "runtime-host.snapshot.read",
        "property.cached.read",
        "property.authoritative.read",
        "property.write",
        "command.execute",
        "observation.subscribe")
    $clientPrincipals = @(
        $enrollment.enrollments |
            Where-Object {
                $_.principalId -ne "hase-python-automation" `
                    -and $_.principalId -ne $laptopPrincipal
            } |
            ForEach-Object { [string]$_.principalId } |
            Sort-Object -Unique)
    if ($clientPrincipals.Count -lt 1) { throw "client-state" }
    foreach ($principal in $clientPrincipals)
    {
        $permissions = @(
            $authorization.grants |
                Where-Object { $_.principalId -eq $principal } |
                ForEach-Object { [string]$_.permission } |
                Sort-Object -Unique)
        if ($permissions.Count -ne 6)
        { throw "client-state" }
        foreach ($permission in $clientPermissions)
        {
            if ($permissions -notcontains $permission) { throw "client-state" }
        }
    }

    $staging = Resolve-AbsolutePath $StagingDirectory
    $certificate = Resolve-AbsolutePath $CertificatePath
    $privateKey = Resolve-AbsolutePath $PrivateKeyPath
    $profile = Resolve-AbsolutePath $ProfilePath
    $transfer = Resolve-AbsolutePath $TransferArchivePath
    $rollback = Resolve-AbsolutePath $RollbackDirectory
    $outputs = @($staging, $certificate, $privateKey, $profile, $transfer, $rollback)
    if (@($outputs | Sort-Object -Unique).Count -ne $outputs.Count) { throw "outputs" }
    foreach ($output in $outputs)
    {
        if (Test-Path -LiteralPath $output) { throw "outputs" }
        if (Test-Within $repositoryRoot $output) { throw "outputs" }
        $parent = Split-Path -Parent $output
        if ([string]::Equals($parent, $staging, [System.StringComparison]::OrdinalIgnoreCase))
        {
            Assert-NoReparsePoint (Split-Path -Parent $staging)
        }
        else
        {
            if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "outputs" }
            Assert-NoReparsePoint $parent
        }
    }
    foreach ($leaf in @($certificate, $privateKey, $profile))
    {
        if (-not (Test-Within $staging $leaf)) { throw "staging-custody" }
    }
    if ((Test-Within $staging $transfer) -or (Test-Within $staging $rollback))
    { throw "external-custody" }

    Write-Host "Repository baseline ready       : True"
    Write-Host "Runtime processes stopped       : True"
    Write-Host "Dedicated authority ready       : True"
    Write-Host "Laptop principal absent         : True"
    Write-Host "MiniPC local Python preserved   : True"
    Write-Host "Existing Client access ready    : True"
    Write-Host "Two minimal grants planned      : True"
    Write-Host "Staging targets absent          : True"
    Write-Host "External rollback target absent : True"
    Write-Host "MiniPC Laptop credential ready  : True"
}
catch
{
    Write-Error "MiniPC Laptop Python credential readiness failed."
    exit 1
}
