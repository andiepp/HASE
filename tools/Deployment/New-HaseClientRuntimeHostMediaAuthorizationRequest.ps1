[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [string]$RepositoryPath = "H:\Development",
    [string]$OutputPath = $(Join-Path $env:LOCALAPPDATA `
        "HASE\Client\Preparation\runtime-host-media-authorization-request.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

if ($env:COMPUTERNAME -cne "LTAEP") {
    throw "Run this tool only on LTAEP."
}

[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
Assert-HaseApplicationsStopped

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not [System.IO.Path]::IsPathRooted($OutputPath) -or
    $OutputPath -match '^[A-Za-z]:[^\\/]') {
    throw "The authorization-request output path must be fully qualified."
}
if (Test-Path -LiteralPath $outputFullPath) {
    throw "The authorization-request output already exists."
}

$registryPath = Join-Path $env:LOCALAPPDATA `
    "HASE\Client\Configuration\client-runtime-hosts.json"
$registry = Read-HaseBoundedJson $registryPath `
    "Client Runtime Host registry"
Assert-HaseExactProperties $registry @("formatVersion", "hosts") `
    "Client Runtime Host registry"
$profiles = @($registry.hosts)
if ([int]$registry.formatVersion -ne 1 -or $profiles.Count -lt 1) {
    throw "The Client Runtime Host registry is incomplete."
}

$requestProfiles = New-Object System.Collections.Generic.List[object]
$hostIds = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
foreach ($profile in $profiles) {
    if ($null -eq $profile -or -not [bool]$profile.enabled) {
        continue
    }
    if ([string]::IsNullOrWhiteSpace(
            [string]$profile.expectedRuntimeHostId) -or
        [string]::IsNullOrWhiteSpace(
            [string]$profile.privateNetworkConfigurationFilePath)) {
        throw "An enabled Client Runtime Host profile is incomplete."
    }
    if (-not $hostIds.Add([string]$profile.expectedRuntimeHostId)) {
        throw "Enabled Client profiles contain a duplicate Runtime Host identity."
    }

    $clientConfiguration = Read-HaseBoundedJson `
        ([string]$profile.privateNetworkConfigurationFilePath) `
        "Client private-network configuration"
    if ([int]$clientConfiguration.formatVersion -ne 1 -or
        -not (Test-HaseHasProperty $clientConfiguration `
            "clientCertificate")) {
        throw "An enabled Client private-network configuration is incomplete."
    }
    $reference = $clientConfiguration.clientCertificate
    if ([string]$reference.storeLocation -cne "CurrentUser" -or
        [string]::IsNullOrWhiteSpace([string]$reference.storeName) -or
        [string]::IsNullOrWhiteSpace([string]$reference.thumbprint)) {
        throw "A Client certificate reference is invalid."
    }
    $storePath = "Cert:\CurrentUser\" + [string]$reference.storeName
    $certificates = @(Get-ChildItem -LiteralPath $storePath |
        Where-Object {
            [string]::Equals(
                $_.Thumbprint,
                [string]$reference.thumbprint,
                [System.StringComparison]::OrdinalIgnoreCase)
        })
    if ($certificates.Count -ne 1 -or
        -not $certificates[0].HasPrivateKey) {
        throw "A Client certificate is not uniquely ready."
    }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $certificateHash = $sha.ComputeHash($certificates[0].RawData)
    }
    finally {
        $sha.Dispose()
    }
    $credentialId = "x509-sha256:" +
        [System.BitConverter]::ToString($certificateHash).
            Replace("-", "").ToLowerInvariant()
    $requestProfiles.Add([ordered]@{
        expectedRuntimeHostId = [string]$profile.expectedRuntimeHostId
        credentialId = $credentialId
    })
}
if ($requestProfiles.Count -lt 1 -or $requestProfiles.Count -gt 16) {
    throw "The enabled Client Runtime Host profile count is unsupported."
}

$outputDirectory = Split-Path -Parent $outputFullPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $outputDirectory)
}
$directoryAcl = Get-Acl -LiteralPath $outputDirectory
$directoryAcl.SetAccessRuleProtection($true, $false)
$directoryAcl.SetAccessRule(
    (New-Object System.Security.AccessControl.FileSystemAccessRule(
        [System.Security.Principal.WindowsIdentity]::GetCurrent().Name,
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow")))
$directoryAcl.SetAccessRule(
    (New-Object System.Security.AccessControl.FileSystemAccessRule(
        "SYSTEM",
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow")))
Set-Acl -LiteralPath $outputDirectory -AclObject $directoryAcl

Write-HaseUtf8Json $outputFullPath ([ordered]@{
    formatVersion = 1
    profiles = @($requestProfiles)
})
$requestHash = Get-HaseRequiredFileHash $outputFullPath `
    "Client media authorization request"

Write-Host ""
Write-Host "ADR-0055 Client media authorization request prepared"
Write-Host ""
Write-Host "Computer exact             :" ($env:COMPUTERNAME -ceq "LTAEP")
Write-Host "Repository commit exact    :" $true
Write-Host "Enabled profile count      :" $requestProfiles.Count
Write-Host "Output path                 :" $outputFullPath
Write-Host "Output SHA-256              :" $requestHash
Write-Host "Certificate values withheld:" $true
Write-Host ""
Write-Host "No certificate, credential, profile, authorization, application,"
Write-Host "deployment, device, signaling, serial, firmware, or physical state"
Write-Host "was changed."
