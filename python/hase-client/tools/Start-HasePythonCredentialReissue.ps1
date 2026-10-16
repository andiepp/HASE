<#
.SYNOPSIS
Re-issues an expired Python automation credential of a colocated Runtime
Host (ADR-0083).

.DESCRIPTION
Reads the installed Runtime Host's configuration and the local Python
profile, then reports the credential's identity, dates and state, the
principal's grants, the signing root and the custody state. With
-PreflightOnly it changes nothing and exits 0 only when a re-issue may run.

Without -PreflightOnly it requires the same readiness, then runs the
credential-provisioning operator's reissue-expired with every path and
hash locked, and verifies the result with
Test-HasePythonCredentialReissue.ps1. The exact old files stay beside
their targets until Recover-HasePythonCredentialReissue.ps1 removes them.

It prints credential identifiers, dates and states only, never the content
of a certificate or a key.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ExpectedComputer,
    [string] $RuntimeHostConfigurationPath = (Join-Path $env:LOCALAPPDATA `
        "HASE\RuntimeHost\Configuration\desktop-runtime-host.json"),
    [string] $PythonProfilePath = (Join-Path $env:LOCALAPPDATA `
        "HASE\PythonAutomation\Security\runtime-host-profile.json"),
    [string] $PrincipalId = "hase-python-automation",
    [ValidateRange(1, 90)] [int] $ValidityDays = 90,
    [string] $SigningRootThumbprint = "",
    [switch] $PreflightOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-HaseFullPath([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim() -or
        $Value -notmatch '^[A-Za-z]:[\\/]')
    {
        throw "path"
    }
    return [IO.Path]::GetFullPath($Value)
}

function Get-HaseFileSha256([string] $Path)
{
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-HaseCredentialId([Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)
{
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        $hash = $sha.ComputeHash($Certificate.RawData)
        return "x509-sha256:" + [BitConverter]::ToString($hash).Replace("-", "").ToLowerInvariant()
    }
    finally
    {
        $sha.Dispose()
    }
}

function Get-HaseShortId([string] $CredentialId)
{
    if ($CredentialId.Length -le 24) { return $CredentialId }
    return $CredentialId.Substring(0, 24) + "..."
}

function Get-HaseReissueState
{
    $state = [ordered]@{}
    $state.ComputerMatches = ($env:OS -eq "Windows_NT") -and
        ($env:COMPUTERNAME -ceq $ExpectedComputer)

    $hostConfiguration = Get-HaseFullPath $RuntimeHostConfigurationPath
    $profilePath = Get-HaseFullPath $PythonProfilePath
    $hostDocument = Get-Content -LiteralPath $hostConfiguration -Raw | ConvertFrom-Json
    $state.PolicyPath = Get-HaseFullPath ([string]$hostDocument.authorizationPolicyFilePath)
    $network = Get-Content -LiteralPath (Get-HaseFullPath `
        ([string]$hostDocument.privateNetworkConfigurationFilePath)) -Raw | ConvertFrom-Json
    $state.EnrollmentPath = Get-HaseFullPath ([string]$network.clientEnrollmentFilePath)

    $profileDocument = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    $state.ProfilePath = $profilePath
    $state.Directory = Split-Path -Parent $profilePath
    $state.CertificatePath = Get-HaseFullPath `
        ([string]$profileDocument.clientCertificate.certificateChainPath)
    $state.PrivateKeyPath = Get-HaseFullPath `
        ([string]$profileDocument.clientCertificate.privateKeyPath)
    $state.TrustedServerPath = Get-HaseFullPath `
        ([string]$profileDocument.trustedServerCertificate.certificatePath)
    foreach ($path in @($state.PolicyPath, $state.EnrollmentPath,
        $state.CertificatePath, $state.PrivateKeyPath, $state.TrustedServerPath))
    {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "input" }
    }

    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $state.CertificatePath)
    try
    {
        $state.CredentialId = Get-HaseCredentialId $certificate
        $state.NotBefore = $certificate.NotBefore.ToUniversalTime()
        $state.NotAfter = $certificate.NotAfter.ToUniversalTime()
        $state.Issuer = $certificate.Issuer
    }
    finally
    {
        $certificate.Dispose()
    }
    $now = [DateTime]::UtcNow
    if ($now -ge $state.NotAfter) { $state.SourceState = "Expired" }
    elseif ($now -lt $state.NotBefore) { $state.SourceState = "NotYetValid" }
    else { $state.SourceState = "Valid" }

    $registry = Get-Content -LiteralPath $state.EnrollmentPath -Raw | ConvertFrom-Json
    $current = @($registry.enrollments | Where-Object {
        [string]$_.credentialId -ceq $state.CredentialId
    })
    $state.Enrolled = $current.Count -eq 1 -and
        [string]$current[0].principalId -ceq $PrincipalId
    $state.TrustPolicyId = ""
    if ($state.Enrolled) { $state.TrustPolicyId = [string]$current[0].trustPolicyId }

    $policy = Get-Content -LiteralPath $state.PolicyPath -Raw | ConvertFrom-Json
    $state.Grants = @($policy.grants | Where-Object {
        [string]$_.principalId -ceq $PrincipalId
    } | ForEach-Object { [string]$_.permission })

    $roots = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.HasPrivateKey -and
        (($SigningRootThumbprint -ne "" -and $_.Thumbprint -ieq $SigningRootThumbprint) -or
         ($SigningRootThumbprint -eq "" -and $_.Subject -ceq $state.Issuer))
    })
    $state.RootFound = $roots.Count -eq 1
    $state.RootThumbprint = ""
    $state.RootCovers = $false
    if ($state.RootFound)
    {
        $state.RootThumbprint = $roots[0].Thumbprint
        $state.RootNotAfter = $roots[0].NotAfter.ToUniversalTime()
        $state.RootCovers = $state.RootNotAfter -gt
            $now.AddMinutes(-5).AddDays($ValidityDays)
    }

    $state.ReissueJournal = Test-Path -LiteralPath (Join-Path $state.Directory `
        "python-credential-reissue.transaction.json")
    $state.RotationJournal = Test-Path -LiteralPath (Join-Path $state.Directory `
        "python-credential-rotation.transaction.json")
    $state.Stopped = @(Get-Process -Name "Hase.DesktopHost.App*", "Hase.Client.Wpf.App*" `
        -ErrorAction SilentlyContinue).Count -eq 0

    $state.Repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
    $status = @(& git -C $state.Repository status --porcelain)
    $head = (& git -C $state.Repository rev-parse HEAD).Trim()
    $origin = (& git -C $state.Repository rev-parse origin/main).Trim()
    $state.RepositoryLevel = $status.Count -eq 0 -and $head -ceq $origin

    $state.Ready = $state.ComputerMatches -and $state.SourceState -eq "Expired" -and
        $state.Enrolled -and $state.Grants.Count -gt 0 -and $state.RootFound -and
        $state.RootCovers -and -not $state.ReissueJournal -and
        -not $state.RotationJournal -and $state.Stopped -and $state.RepositoryLevel
    return $state
}

function Write-HaseReissueState($State)
{
    Write-Host ("Computer matches            : {0}" -f $State.ComputerMatches)
    Write-Host ("Principal                   : {0}" -f $PrincipalId)
    Write-Host ("Current credential          : {0}" -f (Get-HaseShortId $State.CredentialId))
    Write-Host ("Valid from (UTC)            : {0:yyyy-MM-dd HH:mm}" -f $State.NotBefore)
    Write-Host ("Valid until (UTC)           : {0:yyyy-MM-dd HH:mm}" -f $State.NotAfter)
    Write-Host ("Source state                : {0}" -f $State.SourceState)
    Write-Host ("Enrolled for the principal  : {0}" -f $State.Enrolled)
    Write-Host ("Grants                      : {0}" -f $State.Grants.Count)
    if ($State.RootFound)
    {
        Write-Host ("Signing root                : {0} until {1:yyyy-MM-dd}" -f `
            $State.RootThumbprint, $State.RootNotAfter)
    }
    else
    {
        Write-Host "Signing root                : not found"
    }
    Write-Host ("Root covers {0,2} days        : {1}" -f $ValidityDays, $State.RootCovers)
    Write-Host ("Re-issue journal present    : {0}" -f $State.ReissueJournal)
    Write-Host ("Rotation journal present    : {0}" -f $State.RotationJournal)
    Write-Host ("Host and Client stopped     : {0}" -f $State.Stopped)
    Write-Host ("Repository level with origin: {0}" -f $State.RepositoryLevel)
    Write-Host ("Ready for re-issue          : {0}" -f $State.Ready)
}

try
{
    $state = Get-HaseReissueState
    Write-HaseReissueState $state
    if ($PreflightOnly)
    {
        if ($state.Ready) { exit 0 }
        exit 1
    }
    if (-not $state.Ready) { throw "not-ready" }

    $hashes = [ordered]@{
        Certificate = Get-HaseFileSha256 $state.CertificatePath
        PrivateKey = Get-HaseFileSha256 $state.PrivateKeyPath
        Profile = Get-HaseFileSha256 $state.ProfilePath
        Enrollment = Get-HaseFileSha256 $state.EnrollmentPath
        Policy = Get-HaseFileSha256 $state.PolicyPath
        TrustedServer = Get-HaseFileSha256 $state.TrustedServerPath
    }
    $project = Join-Path $state.Repository `
        "src\Hase.Python.CredentialProvisioning.Operator\Hase.Python.CredentialProvisioning.Operator.csproj"
    $arguments = @(
        "run", "--project", $project, "--configuration", "Release", "--verbosity", "quiet", "--",
        "reissue-expired",
        "--signing-root-thumbprint", $state.RootThumbprint,
        "--validity-days", [string]$ValidityDays,
        "--principal-id", $PrincipalId,
        "--trust-policy-id", $state.TrustPolicyId,
        "--expected-grants", ($state.Grants -join ","),
        "--expected-current-credential-id", $state.CredentialId,
        "--expected-trusted-server-certificate-sha256", $hashes.TrustedServer,
        "--provisioning-directory", $state.Directory,
        "--certificate", $state.CertificatePath,
        "--private-key", $state.PrivateKeyPath,
        "--profile", $state.ProfilePath,
        "--enrollment", $state.EnrollmentPath,
        "--authorization-policy", $state.PolicyPath,
        "--expected-certificate-sha256", $hashes.Certificate,
        "--expected-private-key-sha256", $hashes.PrivateKey,
        "--expected-profile-sha256", $hashes.Profile,
        "--expected-enrollment-sha256", $hashes.Enrollment,
        "--expected-authorization-policy-sha256", $hashes.Policy)
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "operator" }

    & (Join-Path $PSScriptRoot "Test-HasePythonCredentialReissue.ps1") `
        -RuntimeHostConfigurationPath $RuntimeHostConfigurationPath `
        -PythonProfilePath $PythonProfilePath `
        -PrincipalId $PrincipalId
    if ($LASTEXITCODE -ne 0) { throw "verification" }
    Write-Host "Expired credential re-issued: True"
    exit 0
}
catch
{
    Write-Error "Python credential re-issue failed; read the state before any recovery."
    exit 1
}
