[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [string]$RepositoryPath = "H:\Development",
    [string]$OutputPath = $(Join-Path $env:LOCALAPPDATA `
        "HASE\Client\Preparation\runtime-host-media-authorization-request.json"),
    [Parameter(Mandatory = $true)] [string] $ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

function Test-HaseAuthorizationRequestDirectoryAcl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [System.Security.Principal.SecurityIdentifier]$CurrentUserSid
    )

    $directoryInfo = New-Object System.IO.DirectoryInfo($Path)
    $directorySecurity = $directoryInfo.GetAccessControl(
        [System.Security.AccessControl.AccessControlSections]::Access)
    if (-not $directorySecurity.AreAccessRulesProtected) {
        return $false
    }

    $rules = @($directorySecurity.GetAccessRules(
        $true,
        $false,
        [System.Security.Principal.SecurityIdentifier]))
    if ($rules.Count -ne 2) {
        return $false
    }

    $systemSid = New-Object `
        System.Security.Principal.SecurityIdentifier("S-1-5-18")
    $expectedSids = @(
        $CurrentUserSid.Value,
        $systemSid.Value
    ) | Sort-Object
    $expectedInheritance = (
        [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
        [System.Security.AccessControl.InheritanceFlags]::ObjectInherit)
    $actualSids = New-Object System.Collections.Generic.List[string]
    foreach ($rule in $rules) {
        if ($rule.IsInherited -or
            $rule.AccessControlType -ne
                [System.Security.AccessControl.AccessControlType]::Allow -or
            $rule.FileSystemRights -ne
                [System.Security.AccessControl.FileSystemRights]::FullControl -or
            $rule.InheritanceFlags -ne $expectedInheritance -or
            $rule.PropagationFlags -ne
                [System.Security.AccessControl.PropagationFlags]::None) {
            return $false
        }
        $actualSids.Add($rule.IdentityReference.Value)
    }
    $actualSidArray = @($actualSids.ToArray() | Sort-Object)
    for ($index = 0; $index -lt $expectedSids.Count; $index++) {
        if ($actualSidArray[$index] -cne $expectedSids[$index]) {
            return $false
        }
    }
    return $true
}

function Set-HaseAuthorizationRequestDirectoryAcl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [System.Security.Principal.SecurityIdentifier]$CurrentUserSid
    )

    $directorySecurity = New-Object `
        System.Security.AccessControl.DirectorySecurity
    $directorySecurity.SetAccessRuleProtection($true, $false)
    $systemSid = New-Object `
        System.Security.Principal.SecurityIdentifier("S-1-5-18")
    $inheritance = (
        [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
        [System.Security.AccessControl.InheritanceFlags]::ObjectInherit)
    foreach ($sid in @($CurrentUserSid, $systemSid)) {
        $rule = New-Object `
            System.Security.AccessControl.FileSystemAccessRule(
                $sid,
                [System.Security.AccessControl.FileSystemRights]::FullControl,
                $inheritance,
                [System.Security.AccessControl.PropagationFlags]::None,
                [System.Security.AccessControl.AccessControlType]::Allow)
        $directorySecurity.AddAccessRule($rule)
    }
    $directoryInfo = New-Object System.IO.DirectoryInfo($Path)
    $directoryInfo.SetAccessControl($directorySecurity)
}

if ($env:COMPUTERNAME -cne $ExpectedComputer) {
    throw "Run this tool only on $ExpectedComputer."
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
$directoryAlreadyExisted = Test-Path -LiteralPath $outputDirectory `
    -PathType Container
$currentUserSid = (
    [System.Security.Principal.WindowsIdentity]::GetCurrent()).User
if ($directoryAlreadyExisted) {
    if (-not (Test-HaseAuthorizationRequestDirectoryAcl `
            $outputDirectory $currentUserSid)) {
        throw "The existing authorization-request directory permissions are not exact."
    }
}
else {
    [void](New-Item -ItemType Directory -Path $outputDirectory)
    Set-HaseAuthorizationRequestDirectoryAcl `
        $outputDirectory $currentUserSid
}
if (-not (Test-HaseAuthorizationRequestDirectoryAcl `
        $outputDirectory $currentUserSid)) {
    throw "The authorization-request directory permissions are not exact."
}

Write-HaseUtf8Json $outputFullPath ([ordered]@{
    formatVersion = 1
    profiles = $requestProfiles.ToArray()
})
$requestHash = Get-HaseRequiredFileHash $outputFullPath `
    "Client media authorization request"

Write-Host ""
Write-Host "ADR-0055 Client media authorization request prepared"
Write-Host ""
Write-Host "Computer exact             :" ($env:COMPUTERNAME -ceq $ExpectedComputer)
Write-Host "Repository commit exact    :" $true
Write-Host "Enabled profile count      :" $requestProfiles.Count
Write-Host "Output path                 :" $outputFullPath
Write-Host "Output SHA-256              :" $requestHash
Write-Host "Protected directory reused :" $directoryAlreadyExisted
Write-Host "Protected directory exact  :" $true
Write-Host "Certificate values withheld:" $true
Write-Host ""
Write-Host "No certificate, credential, profile, authorization, application,"
Write-Host "deployment, device, signaling, serial, firmware, or physical state"
Write-Host "was changed."
