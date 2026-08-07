[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Read-PolicyKeys {
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
    $keys = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($grant in $document.grants) {
        if ($null -eq $grant -or
            [string]::IsNullOrWhiteSpace([string]$grant.principalId) -or
            [string]::IsNullOrWhiteSpace([string]$grant.permission)) {
            throw "The $Role contains an invalid grant."
        }
        $principal = [string]$grant.principalId
        $key = $principal.Length.ToString(
            [System.Globalization.CultureInfo]::InvariantCulture) + ":" +
            $principal + [string]$grant.permission
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
    throw "Stop the HASE Desktop Runtime Host before restoring its authorization policy."
}
foreach ($requiredFile in @($profilePath, $installedPolicyPath, $authorizedBackupPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The authorization-policy restoration prerequisites are incomplete."
    }
}
foreach ($prohibitedArtifact in @($deniedBackupPath, $temporaryPath)) {
    if (Test-Path -LiteralPath $prohibitedArtifact) {
        throw "The authorization-policy restoration target is not clean."
    }
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

$denied = Read-PolicyKeys $installedPolicyPath "installed alternative authorization policy"
$authorized = Read-PolicyKeys $authorizedBackupPath "retained authorized policy"
$restored = @($authorized.Keys | Where-Object { -not $denied.Keys.Contains($_) })
$unexpected = @($denied.Keys | Where-Object { -not $authorized.Keys.Contains($_) })
if ($restored.Count -ne 1 -or $unexpected.Count -ne 0 -or
    -not $restored[0].EndsWith("diagnostics.subscribe", [System.StringComparison]::Ordinal)) {
    throw "The retained policies do not represent the supported diagnostics-only substitution."
}

$replaced = $false
try {
    [System.IO.File]::Replace(
        $authorizedBackupPath,
        $installedPolicyPath,
        $deniedBackupPath,
        $true)
    $replaced = $true
    if ((Get-FileHash -LiteralPath $installedPolicyPath -Algorithm SHA256).Hash -cne
            $authorized.Hash -or
        (Get-FileHash -LiteralPath $deniedBackupPath -Algorithm SHA256).Hash -cne
            $denied.Hash) {
        throw "Authorization-policy restoration custody verification failed."
    }
}
catch {
    if ($replaced -and (Test-Path -LiteralPath $deniedBackupPath -PathType Leaf)) {
        [System.IO.File]::Replace(
            $deniedBackupPath,
            $installedPolicyPath,
            $authorizedBackupPath,
            $true)
    }
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
    throw
}

Write-Host "HASE Runtime Host authorization-policy restoration succeeded."
Write-Host "Authorized policy : restored exactly"
Write-Host "Alternative policy: backup retained"
Write-Host "Runtime Host       : remains stopped"
Write-Host "Sensitive values   : withheld"
