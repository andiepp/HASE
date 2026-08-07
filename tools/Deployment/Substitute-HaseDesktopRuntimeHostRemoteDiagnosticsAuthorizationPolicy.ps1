[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationPolicyPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-FullyQualifiedFilePath {
    param([string]$Path, [string]$Role)
    if ([string]::IsNullOrWhiteSpace($Path) -or
        -not [System.IO.Path]::IsPathRooted($Path) -or
        $Path -match '^[A-Za-z]:[^\\/]') {
        throw "The $Role path must be fully qualified."
    }
    return [System.IO.Path]::GetFullPath($Path)
}

function Read-Policy {
    param([string]$Path, [string]$Role)
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -gt (64 * 1024)) {
        throw "The $Role exceeds the supported size."
    }
    try {
        $document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 |
            ConvertFrom-Json
    }
    catch {
        throw "The $Role is not valid JSON configuration."
    }
    $properties = @($document.PSObject.Properties.Name)
    if ($null -eq $document -or $properties.Count -ne 2 -or
        $properties -notcontains "formatVersion" -or
        $properties -notcontains "grants" -or
        $document.formatVersion -ne 1 -or
        $document.grants -isnot [System.Array]) {
        throw "The $Role does not have the supported structure."
    }
    $supportedPermissions = @(
        "runtime-host.snapshot.read",
        "property.cached.read",
        "property.authoritative.read",
        "property.write",
        "command.execute",
        "observation.subscribe",
        "diagnostics.subscribe")
    $keys = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($grant in $document.grants) {
        if ($null -eq $grant) {
            throw "The $Role contains an invalid grant."
        }
        $grantProperties = @($grant.PSObject.Properties.Name)
        if ($grantProperties.Count -ne 2 -or
            $grantProperties -notcontains "principalId" -or
            $grantProperties -notcontains "permission" -or
            [string]::IsNullOrWhiteSpace([string]$grant.principalId) -or
            $supportedPermissions -cnotcontains [string]$grant.permission) {
            throw "The $Role contains an invalid grant."
        }
        $principal = [string]$grant.principalId
        $permission = [string]$grant.permission
        $key = $principal.Length.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture) + ":" +
            $principal + $permission
        if (-not $keys.Add($key)) {
            throw "The $Role contains a duplicate grant."
        }
    }
    return [pscustomobject]@{
        Keys = $keys
        Hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }
}

$configurationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost\Configuration"
$profilePath = Join-Path $configurationDirectory "desktop-runtime-host.json"
$installedPolicyPath = Join-Path $configurationDirectory "runtime-host-authorization.json"
$authorizedBackupPath = $installedPolicyPath + ".49o4-authorized-backup"
$deniedBackupPath = $installedPolicyPath + ".49o4-denied-backup"
$temporaryPath = $installedPolicyPath + ".49o4-tmp"

if ($null -ne (Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue)) {
    throw "Stop the HASE Desktop Runtime Host before substituting its authorization policy."
}
foreach ($requiredFile in @($profilePath, $installedPolicyPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The migrated Runtime Host authorization-policy installation is incomplete."
    }
}
foreach ($prohibitedArtifact in @($authorizedBackupPath, $deniedBackupPath, $temporaryPath)) {
    if (Test-Path -LiteralPath $prohibitedArtifact) {
        throw "The authorization-policy substitution target is not clean."
    }
}

$candidatePath = Get-FullyQualifiedFilePath $AuthorizationPolicyPath "alternative authorization-policy"
if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
    throw "The alternative authorization-policy source does not exist."
}
if ([string]::Equals($candidatePath, $installedPolicyPath,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The alternative authorization-policy source must be separate from the installed policy."
}

$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$profileProperties = @($profile.PSObject.Properties.Name)
if ($profileProperties -notcontains "remoteDiagnosticsEnabled" -or
    $profile.remoteDiagnosticsEnabled -ne $true -or
    $profileProperties -notcontains "authorizationPolicyFilePath" -or
    -not [string]::Equals(
        [System.IO.Path]::GetFullPath([string]$profile.authorizationPolicyFilePath),
        [System.IO.Path]::GetFullPath($installedPolicyPath),
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The active Runtime Host profile is not a completed remote-diagnostics migration."
}

$installed = Read-Policy $installedPolicyPath "installed authorization policy"
$candidate = Read-Policy $candidatePath "alternative authorization policy"
$removed = @($installed.Keys | Where-Object { -not $candidate.Keys.Contains($_) })
$added = @($candidate.Keys | Where-Object { -not $installed.Keys.Contains($_) })
if ($removed.Count -ne 1 -or $added.Count -ne 0 -or
    -not $removed[0].EndsWith("diagnostics.subscribe", [System.StringComparison]::Ordinal)) {
    throw "The alternative policy must remove exactly one diagnostics.subscribe grant and preserve every other grant."
}

$replaced = $false
try {
    [System.IO.File]::WriteAllBytes(
        $temporaryPath,
        [System.IO.File]::ReadAllBytes($candidatePath))
    if ((Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash -cne
        $candidate.Hash) {
        throw "The staged alternative authorization policy did not match its validated source."
    }
    [System.IO.File]::Replace(
        $temporaryPath,
        $installedPolicyPath,
        $authorizedBackupPath,
        $true)
    $replaced = $true
    if ((Get-FileHash -LiteralPath $installedPolicyPath -Algorithm SHA256).Hash -cne
            $candidate.Hash -or
        (Get-FileHash -LiteralPath $authorizedBackupPath -Algorithm SHA256).Hash -cne
            $installed.Hash) {
        throw "Authorization-policy substitution custody verification failed."
    }
}
catch {
    if ($replaced -and (Test-Path -LiteralPath $authorizedBackupPath -PathType Leaf)) {
        [System.IO.File]::Replace(
            $authorizedBackupPath,
            $installedPolicyPath,
            $temporaryPath,
            $true)
    }
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
    throw
}

Write-Host "HASE Runtime Host authorization-policy substitution succeeded."
Write-Host "Authorized policy : backup retained"
Write-Host "Alternative policy: installed"
Write-Host "Runtime Host       : remains stopped"
Write-Host "Sensitive values   : withheld"
