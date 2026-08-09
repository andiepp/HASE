[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $MiniPcConfigurationPath,

    [Parameter(Mandatory = $true)]
    [string] $TrustedServerCertificatePath,

    [Parameter(Mandatory = $true)]
    [string] $ApplicationProfilePath,

    [Parameter(Mandatory = $true)]
    [string] $ProvisioningDirectory,

    [Parameter(Mandatory = $true)]
    [string] $ProfileTemplatePath,

    [Parameter(Mandatory = $true)]
    [string] $CertificatePath,

    [Parameter(Mandatory = $true)]
    [string] $PrivateKeyPath,

    [Parameter(Mandatory = $true)]
    [string] $ProfilePath,

    [Parameter(Mandatory = $true)]
    [string] $RollbackDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-ExactAbsolutePath
{
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value) `
        -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]' `
            -or $Value -match '^\\\\[^\\/]+[\\/][^\\/]+(?:[\\/]|$)'))
    {
        throw "path-invalid"
    }
    return [System.IO.Path]::GetFullPath($Value)
}

function Test-PathWithin
{
    param([string] $Parent, [string] $Candidate)

    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd("\") + "\"
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate)
    return $candidatePath.StartsWith(
        $parentPath,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePoint
{
    param([string] $Path)

    $current = if (Test-Path -LiteralPath $Path) {
        $Path
    } else {
        Split-Path -Parent $Path
    }
    while (-not [string]::IsNullOrWhiteSpace($current))
    {
        if (Test-Path -LiteralPath $current)
        {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) `
                -ne 0)
            {
                throw "reparse-point"
            }
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
    $repositoryRoot = Resolve-ExactAbsolutePath (
        Split-Path -Parent (Split-Path -Parent $packageDirectory))

    $configurationPath = Resolve-ExactAbsolutePath $MiniPcConfigurationPath
    $trustedCertificatePath = Resolve-ExactAbsolutePath (
        $TrustedServerCertificatePath)
    $applicationProfilePath = Resolve-ExactAbsolutePath $ApplicationProfilePath
    $provisioningRoot = Resolve-ExactAbsolutePath $ProvisioningDirectory
    $templatePath = Resolve-ExactAbsolutePath $ProfileTemplatePath
    $certificateOutput = Resolve-ExactAbsolutePath $CertificatePath
    $privateKeyOutput = Resolve-ExactAbsolutePath $PrivateKeyPath
    $profileOutput = Resolve-ExactAbsolutePath $ProfilePath
    $rollbackPath = Resolve-ExactAbsolutePath $RollbackDirectory

    $status = @(& git -C $repositoryRoot status --porcelain)
    if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0) { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }

    $runtimeProcesses = @(
        Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue)
    $clientProcesses = @(
        Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue)
    if ($runtimeProcesses.Count -ne 0 -or $clientProcesses.Count -ne 0)
    {
        throw "processes"
    }

    $readinessScript = Join-Path $toolDirectory (
        "Test-HasePythonCredentialProvisioningReadiness.ps1")
    $windowsPowerShell = Join-Path `
        ([System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::System)) `
        "WindowsPowerShell\v1.0\powershell.exe"
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass `
        -File $readinessScript `
        -DesktopConfigurationPath $configurationPath `
        1>$null 2>$null 3>$null 4>$null 5>$null 6>$null
    if ($LASTEXITCODE -ne 0) { throw "credential-readiness" }

    foreach ($inputPath in @(
        $configurationPath, $trustedCertificatePath, $applicationProfilePath))
    {
        if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf))
        {
            throw "input"
        }
        Assert-NoReparsePoint $inputPath
    }
    $provisioningParent = Split-Path -Parent $provisioningRoot
    if ((Test-Path -LiteralPath $provisioningRoot) `
        -or -not (Test-Path -LiteralPath $provisioningParent -PathType Container) `
        -or (Test-PathWithin $repositoryRoot $provisioningRoot))
    {
        throw "provisioning-directory"
    }
    Assert-NoReparsePoint $provisioningParent

    $configuration = Get-Content -LiteralPath $configurationPath -Raw |
        ConvertFrom-Json
    $applicationProfile = Get-Content -LiteralPath $applicationProfilePath -Raw |
        ConvertFrom-Json
    $applicationProperties = @($applicationProfile.PSObject.Properties.Name)
    if ($applicationProperties -notcontains "privateNetworkConfigurationFilePath" `
        -or (Resolve-ExactAbsolutePath (
            [string]$applicationProfile.privateNetworkConfigurationFilePath)) `
            -ne $configurationPath)
    {
        throw "application-profile"
    }
    $enrollmentPath = Resolve-ExactAbsolutePath (
        [string]$configuration.clientEnrollmentFilePath)
    if (-not (Test-Path -LiteralPath $enrollmentPath -PathType Leaf))
    {
        throw "enrollment"
    }
    Assert-NoReparsePoint $enrollmentPath
    $enrollment = Get-Content -LiteralPath $enrollmentPath -Raw |
        ConvertFrom-Json
    $existingEnrollment = @(
        $enrollment.enrollments |
            Where-Object { $_.principalId -eq "hase-python-automation" })
    if ($existingEnrollment.Count -ne 0) { throw "python-identity-present" }

    $authorizationPath = $null
    if ($applicationProperties -contains "authorizationPolicyFilePath")
    {
        $authorizationPath = Resolve-ExactAbsolutePath (
            [string]$applicationProfile.authorizationPolicyFilePath)
        if (-not (Test-Path -LiteralPath $authorizationPath -PathType Leaf))
        {
            throw "authorization-policy"
        }
        Assert-NoReparsePoint $authorizationPath
        $authorization = Get-Content -LiteralPath $authorizationPath -Raw |
            ConvertFrom-Json
        $existingGrants = @(
            $authorization.grants |
                Where-Object { $_.principalId -eq "hase-python-automation" })
        if ($existingGrants.Count -ne 0) { throw "python-grants-present" }
    }

    $personalStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::My,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $public = $null
    try
    {
        $personalStore.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        $thumbprint = [string]$configuration.serverCertificate.thumbprint
        $active = @($personalStore.Certificates | Where-Object {
            [string]::Equals($_.Thumbprint, $thumbprint,
                [System.StringComparison]::OrdinalIgnoreCase) })
        $public = Get-PfxCertificate -FilePath $trustedCertificatePath
        if ($active.Count -ne 1 -or $null -eq $public `
            -or $public.HasPrivateKey `
            -or [System.Convert]::ToBase64String($active[0].RawData) -ne `
                [System.Convert]::ToBase64String($public.RawData))
        {
            throw "trusted-certificate"
        }
    }
    finally
    {
        if ($null -ne $personalStore) { $personalStore.Dispose() }
        if ($null -ne $public) { $public.Dispose() }
    }

    $plannedFiles = @(
        $templatePath, $certificateOutput, $privateKeyOutput, $profileOutput)
    $allPaths = @(
        $configurationPath, $trustedCertificatePath, $applicationProfilePath,
        $enrollmentPath, $templatePath, $certificateOutput,
        $privateKeyOutput, $profileOutput, $rollbackPath)
    if ($null -ne $authorizationPath) { $allPaths += $authorizationPath }
    if (@($allPaths | Sort-Object -Unique).Count -ne $allPaths.Count)
    {
        throw "paths-not-distinct"
    }
    foreach ($planned in $plannedFiles)
    {
        $parent = Split-Path -Parent $planned
        if ((Test-Path -LiteralPath $planned) `
            -or (Test-PathWithin $repositoryRoot $planned))
        {
            throw "planned-output"
        }
        if ($planned -eq $templatePath)
        {
            if (-not (Test-Path -LiteralPath $parent -PathType Container))
            {
                throw "planned-output"
            }
            Assert-NoReparsePoint $parent
        }
    }
    foreach ($planned in @(
        $certificateOutput, $privateKeyOutput, $profileOutput))
    {
        if (-not (Test-PathWithin $provisioningRoot $planned))
        {
            throw "output-outside-provisioning"
        }
    }
    if ((Test-PathWithin $provisioningRoot $templatePath))
    {
        throw "template-inside-provisioning"
    }

    $rollbackParent = Split-Path -Parent $rollbackPath
    if ((Test-Path -LiteralPath $rollbackPath) `
        -or -not (Test-Path -LiteralPath $rollbackParent -PathType Container) `
        -or (Test-PathWithin $repositoryRoot $rollbackPath) `
        -or (Test-PathWithin $provisioningRoot $rollbackPath))
    {
        throw "rollback"
    }
    Assert-NoReparsePoint $rollbackParent

    $journals = @()
    $artifacts = @()
    foreach ($target in @(
        $certificateOutput, $privateKeyOutput, $profileOutput,
        $enrollmentPath, $applicationProfilePath))
    {
        $targetParent = Split-Path -Parent $target
        if (Test-Path -LiteralPath $targetParent -PathType Container)
        {
            $artifacts += @(Get-ChildItem -LiteralPath $targetParent `
                -File | Where-Object {
                    $_.FullName.StartsWith($target + ".stage-",
                        [System.StringComparison]::OrdinalIgnoreCase) `
                    -or $_.FullName.StartsWith($target + ".backup-",
                        [System.StringComparison]::OrdinalIgnoreCase) })
        }
    }
    if ($null -ne $authorizationPath)
    {
        $artifacts += @(Get-ChildItem `
            -LiteralPath (Split-Path -Parent $authorizationPath) `
            -File | Where-Object {
                $_.FullName.StartsWith($authorizationPath + ".stage-",
                    [System.StringComparison]::OrdinalIgnoreCase) `
                -or $_.FullName.StartsWith($authorizationPath + ".backup-",
                    [System.StringComparison]::OrdinalIgnoreCase) })
    }
    if ($journals.Count -ne 0 -or $artifacts.Count -ne 0)
    {
        throw "transaction-artifacts"
    }

    Write-Host "Repository baseline ready       : True"
    Write-Host "Runtime processes stopped       : True"
    Write-Host "MiniPC credential readiness     : True"
    Write-Host "Public server certificate ready : True"
    Write-Host "Python identity absent          : True"
    Write-Host "Python grants absent            : True"
    Write-Host "External paths distinct         : True"
    Write-Host "Publication targets absent      : True"
    Write-Host "Transaction artifacts absent    : True"
    Write-Host "MiniPC Python provisioning ready: True"
}
catch
{
    Write-Error "MiniPC Python provisioning readiness failed."
    exit 1
}
