[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $TargetRegistryPath,
    [Parameter(Mandatory = $true)] [string] $RequestDirectory,
    [Parameter(Mandatory = $true)] [string] $RequestPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$phase = "preflight"
$createdDirectory = $false

$principalId = "hase-laptop-python-minipc"
$targetId = "minipc-runtime-host"
$expectedGrants = @(
    "command.execute",
    "observation.subscribe",
    "property.authoritative.read",
    "property.write",
    "runtime-host.snapshot.read")

function Resolve-AbsolutePath([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]')) { throw "path" }
    return [IO.Path]::GetFullPath($Value)
}

function Test-Within([string] $Parent, [string] $Candidate)
{
    $prefix = [IO.Path]::GetFullPath($Parent).TrimEnd("\") + "\"
    return [IO.Path]::GetFullPath($Candidate).StartsWith(
        $prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePoint([string] $Path)
{
    $current = if (Test-Path -LiteralPath $Path) {
        [IO.Path]::GetFullPath($Path)
    } else {
        Split-Path -Parent ([IO.Path]::GetFullPath($Path))
    }
    while (-not [string]::IsNullOrWhiteSpace($current))
    {
        if (Test-Path -LiteralPath $current)
        {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            { throw "reparse" }
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

function Set-PrivateDirectory([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($user)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $user, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"))
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Set-PrivateFile([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = [Security.AccessControl.FileSecurity]::new()
    $acl.SetOwner($user)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $user, "FullControl", "Allow"))
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Get-Sha256([string] $Path)
{
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try
{
    $phase = "machine"
    if ($env:OS -ne "Windows_NT" -or $env:COMPUTERNAME -cne "LTAEP")
    { throw "machine" }

    $phase = "repository"
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $packageDirectory "..\.."))
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $phase = "custody"
    $registryPath = Resolve-AbsolutePath $TargetRegistryPath
    $directory = Resolve-AbsolutePath $RequestDirectory
    $output = Resolve-AbsolutePath $RequestPath
    if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf) `
        -or (Test-Path -LiteralPath $directory) `
        -or (Test-Path -LiteralPath $output) `
        -or -not (Test-Within $directory $output) `
        -or (Test-Within $repositoryRoot $directory))
    { throw "custody" }
    $parent = Split-Path -Parent $directory
    if (-not (Test-Path -LiteralPath $parent -PathType Container))
    { throw "parent" }
    $phase = "reparse"
    Assert-NoReparsePoint $registryPath
    Assert-NoReparsePoint $parent

    $phase = "target"
    $registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
    if ([int]$registry.formatVersion -ne 1) { throw "registry" }
    $targets = @($registry.targets | Where-Object {
        [string]$_.targetId -ceq $targetId })
    if ($targets.Count -ne 1) { throw "target" }
    $profilePath = Resolve-AbsolutePath ([string]$targets[0].profilePath)
    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    if ([int]$profile.formatVersion -ne 1) { throw "profile" }
    $certificatePath = Resolve-AbsolutePath (
        [string]$profile.clientCertificate.certificateChainPath)
    $privateKeyPath = Resolve-AbsolutePath (
        [string]$profile.clientCertificate.privateKeyPath)
    $trustedServerPath = Resolve-AbsolutePath (
        [string]$profile.trustedServerCertificate.certificatePath)
    foreach ($path in @($profilePath, $certificatePath, $privateKeyPath,
            $trustedServerPath))
    {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "input" }
        Assert-NoReparsePoint $path
    }

    $phase = "credential"
    $certificate = Get-PfxCertificate -FilePath $certificatePath
    try
    {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        $certificateDerHash = $null
        try
        {
            $certificateDerHash = $sha256.ComputeHash($certificate.RawData)
            $credentialId = "x509-sha256:" +
                [BitConverter]::ToString($certificateDerHash).
                    Replace("-", "").ToLowerInvariant()
        }
        finally
        {
            if ($null -ne $certificateDerHash)
            { [Array]::Clear($certificateDerHash, 0, $certificateDerHash.Length) }
            $sha256.Dispose()
        }
    }
    finally { $certificate.Dispose() }

    $phase = "request"
    $request = [ordered]@{
        schemaVersion = 1
        purpose = "hase-laptop-minipc-python-cross-computer-rotation-request"
        repositoryHead = $head
        targetId = $targetId
        principalId = $principalId
        expectedCurrentCredentialId = $credentialId
        expectedGrants = $expectedGrants
        profileSha256 = Get-Sha256 $profilePath
        certificateSha256 = Get-Sha256 $certificatePath
        privateKeySha256 = Get-Sha256 $privateKeyPath
        trustedServerCertificateSha256 = Get-Sha256 $trustedServerPath
        createdUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }
    $phase = "directory-publication"
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $createdDirectory = $true
    Set-PrivateDirectory $directory
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
        ($request | ConvertTo-Json -Depth 8))
    try
    {
        $phase = "request-publication"
        [IO.File]::WriteAllBytes($output, $bytes)
        Set-PrivateFile $output
    }
    finally
    {
        [Array]::Clear($bytes, 0, $bytes.Length)
    }

    Write-Host "Repository baseline ready       : True"
    Write-Host "Runtime processes stopped       : True"
    Write-Host "MiniPC target selected exactly  : True"
    Write-Host "Installed credential inputs valid: True"
    Write-Host "Five exact grants recorded      : True"
    Write-Host "Old private key exported        : False"
    Write-Host "Profile content exported        : False"
    Write-Host "Protected request created       : True"
    Write-Host "Deployment state changed        : False"
    Write-Host "Rotation request ready          : True"
}
catch
{
    if ($createdDirectory -and (Test-Path -LiteralPath $directory))
    {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    Write-Error "Laptop MiniPC Python rotation request creation failed at phase '$phase'."
    exit 1
}
