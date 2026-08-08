[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $DesktopConfigurationPath,

    [Parameter(Mandatory = $true)]
    [string] $SourceProfilePath,

    [Parameter(Mandatory = $true)]
    [string] $TrustedServerCertificatePath,

    [Parameter(Mandatory = $true)]
    [string] $AuthorizationPolicyPath,

    [Parameter(Mandatory = $true)]
    [string] $ProvisioningDirectory,

    [Parameter(Mandatory = $true)]
    [string] $CertificatePath,

    [Parameter(Mandatory = $true)]
    [string] $PrivateKeyPath,

    [Parameter(Mandatory = $true)]
    [string] $ProfilePath,

    [Parameter(Mandatory = $true)]
    [string] $RollbackDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 90)]
    [int] $ValidityDays
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rollbackCreated = $false
$sourceProfileCreated = $false
$personalStore = $null
$rootStore = $null
$selectedServerCertificate = $null

function Resolve-ExactAbsolutePath
{
    param([string] $Value)

    if (
        [string]::IsNullOrWhiteSpace($Value) `
        -or $Value -ne $Value.Trim() `
        -or -not (
            $Value -match '^[A-Za-z]:[\\/]' `
            -or $Value -match '^\\\\[^\\/]+[\\/][^\\/]+(?:[\\/]|$)'))
    {
        throw "Invalid"
    }

    return [System.IO.Path]::GetFullPath($Value)
}

function Test-PathWithin
{
    param(
        [string] $Parent,
        [string] $Candidate
    )

    $parentPath = [System.IO.Path]::GetFullPath($Parent)
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate)
    if ($candidatePath.Equals(
        $parentPath,
        [System.StringComparison]::OrdinalIgnoreCase))
    {
        return $true
    }

    if (
        -not $parentPath.EndsWith(
            [System.IO.Path]::DirectorySeparatorChar.ToString()) `
        -and -not $parentPath.EndsWith(
            [System.IO.Path]::AltDirectorySeparatorChar.ToString()))
    {
        $parentPath += [System.IO.Path]::DirectorySeparatorChar
    }

    return $candidatePath.StartsWith(
        $parentPath,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePoint
{
    param([string] $Path)

    $current = $Path
    if (-not (Test-Path -LiteralPath $current))
    {
        $current = Split-Path -Parent $current
    }

    while (-not [string]::IsNullOrWhiteSpace($current))
    {
        if (Test-Path -LiteralPath $current)
        {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
            {
                throw "Invalid"
            }
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current)
        {
            break
        }
        $current = $parent
    }
}

function Get-FileState
{
    param(
        [string] $Name,
        [string] $Path,
        [string] $BackupName
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf))
    {
        return [ordered]@{
            name = $Name
            targetPath = $Path
            existed = $false
            sha256 = $null
            securitySddl = $null
            backupFileName = $null
        }
    }

    Assert-NoReparsePoint -Path $Path
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $security = (Get-Acl -LiteralPath $Path).Sddl
    Copy-Item -LiteralPath $Path -Destination (
        Join-Path $script:rollbackDirectoryPath $BackupName)

    return [ordered]@{
        name = $Name
        targetPath = $Path
        existed = $true
        sha256 = $hash
        securitySddl = $security
        backupFileName = $BackupName
    }
}

function Set-PrivateDirectorySecurity
{
    param([string] $Path)

    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $owner = $identity.User
    if ($null -eq $owner)
    {
        throw "Invalid"
    }

    $security = [System.Security.AccessControl.DirectorySecurity]::new()
    $security.SetOwner($owner)
    $security.SetAccessRuleProtection($true, $false)
    $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
        $owner,
        [System.Security.AccessControl.FileSystemRights]::FullControl,
        [System.Security.AccessControl.InheritanceFlags]::ContainerInherit `
            -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
        [System.Security.AccessControl.PropagationFlags]::None,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $security.AddAccessRule($rule)
    Set-Acl -LiteralPath $Path -AclObject $security
}

function Set-PrivateFileSecurity
{
    param([string] $Path)

    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $owner = $identity.User
    if ($null -eq $owner)
    {
        throw "Invalid"
    }

    $security = [System.Security.AccessControl.FileSecurity]::new()
    $security.SetOwner($owner)
    $security.SetAccessRuleProtection($true, $false)
    $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
        $owner,
        [System.Security.AccessControl.FileSystemRights]::FullControl,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $security.AddAccessRule($rule)
    Set-Acl -LiteralPath $Path -AclObject $security
}

try
{
    if ($env:OS -ne "Windows_NT")
    {
        throw "Unsupported"
    }

    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $pythonDirectory = Split-Path -Parent $packageDirectory
    $repositoryRoot = Resolve-ExactAbsolutePath (
        Split-Path -Parent $pythonDirectory)

    $desktopConfiguration = Resolve-ExactAbsolutePath $DesktopConfigurationPath
    $sourceProfile = Resolve-ExactAbsolutePath $SourceProfilePath
    $trustedServerCertificatePath = Resolve-ExactAbsolutePath (
        $TrustedServerCertificatePath)
    $authorizationPolicy = Resolve-ExactAbsolutePath $AuthorizationPolicyPath
    $provisioningRoot = Resolve-ExactAbsolutePath $ProvisioningDirectory
    $certificate = Resolve-ExactAbsolutePath $CertificatePath
    $privateKey = Resolve-ExactAbsolutePath $PrivateKeyPath
    $profile = Resolve-ExactAbsolutePath $ProfilePath
    $script:rollbackDirectoryPath = Resolve-ExactAbsolutePath $RollbackDirectory

    $status = @(& git -C $repositoryRoot status --porcelain)
    if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0)
    {
        throw "Repository"
    }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin)
    {
        throw "Repository"
    }

    $runtimeProcesses = @(
        Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue
    )
    $clientProcesses = @(
        Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue
    )
    if ($runtimeProcesses.Count -ne 0 -or $clientProcesses.Count -ne 0)
    {
        throw "Processes"
    }

    $readinessScript = Join-Path $toolDirectory (
        "Test-HasePythonCredentialProvisioningReadiness.ps1")
    $windowsPowerShell = Join-Path `
        ([System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::System)) `
        "WindowsPowerShell\v1.0\powershell.exe"
    if (-not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf))
    {
        throw "Readiness"
    }
    & $windowsPowerShell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $readinessScript `
        -DesktopConfigurationPath $desktopConfiguration `
        1>$null 2>$null 3>$null 4>$null 5>$null 6>$null
    if ($LASTEXITCODE -ne 0)
    {
        throw "Readiness"
    }

    foreach ($requiredFile in @(
        $desktopConfiguration,
        $trustedServerCertificatePath,
        $authorizationPolicy))
    {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf))
        {
            throw "Inputs"
        }
        Assert-NoReparsePoint -Path $requiredFile
    }
    if (-not (Test-Path -LiteralPath $provisioningRoot -PathType Container))
    {
        throw "Targets"
    }
    Assert-NoReparsePoint -Path $provisioningRoot

    $sourceProfileParent = Split-Path -Parent $sourceProfile
    if (
        [string]::IsNullOrWhiteSpace($sourceProfileParent) `
        -or -not (Test-Path `
            -LiteralPath $sourceProfileParent `
            -PathType Container) `
        -or (Test-Path -LiteralPath $sourceProfile) `
        -or (Test-PathWithin `
            -Parent $repositoryRoot `
            -Candidate $sourceProfile))
    {
        throw "Inputs"
    }
    Assert-NoReparsePoint -Path $sourceProfileParent

    $configuration = Get-Content -LiteralPath $desktopConfiguration -Raw |
        ConvertFrom-Json
    $bindingAddress = [string]$configuration.binding.address
    $bindingPort = [int]$configuration.binding.port
    $bindingIpAddress = $null
    if (
        [string]::IsNullOrWhiteSpace($bindingAddress) `
        -or -not [System.Net.IPAddress]::TryParse(
            $bindingAddress,
            [ref]$bindingIpAddress) `
        -or $bindingPort -lt 1 `
        -or $bindingPort -gt 65535 `
        -or $bindingPort -eq 443)
    {
        throw "Inputs"
    }
    $authorityHost = $bindingIpAddress.ToString()
    if (
        $bindingIpAddress.AddressFamily -eq
        [System.Net.Sockets.AddressFamily]::InterNetworkV6)
    {
        $authorityHost = "[" + $authorityHost + "]"
    }
    $clientAddress = "https://" + $authorityHost + ":" + $bindingPort
    $serverThumbprint = [string]$configuration.serverCertificate.thumbprint
    $enrollment = Resolve-ExactAbsolutePath (
        [string]$configuration.clientEnrollmentFilePath)
    if (-not (Test-Path -LiteralPath $enrollment -PathType Leaf))
    {
        throw "Inputs"
    }
    Assert-NoReparsePoint -Path $enrollment

    $enrollmentDocument = Get-Content -LiteralPath $enrollment -Raw |
        ConvertFrom-Json
    $trustPolicyIds = @(
        $enrollmentDocument.enrollments |
            ForEach-Object { [string]$_.trustPolicyId } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
    if ($trustPolicyIds.Count -ne 1)
    {
        throw "Inputs"
    }

    $personalStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::My,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $personalStore.Open(
        [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
    $rootStore.Open(
        [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)

    $serverCertificates = @(
        $personalStore.Certificates |
            Where-Object {
                [string]::Equals(
                    $_.Thumbprint,
                    $serverThumbprint,
                    [System.StringComparison]::OrdinalIgnoreCase)
            }
    )
    if ($serverCertificates.Count -ne 1)
    {
        throw "Inputs"
    }
    $selectedServerCertificate = Get-PfxCertificate `
        -FilePath $trustedServerCertificatePath
    $selectedServerCertificateBytes =
        if ($null -eq $selectedServerCertificate)
        {
            [string]::Empty
        }
        else
        {
            [System.Convert]::ToBase64String(
                $selectedServerCertificate.RawData)
        }
    $activeServerCertificateBytes = [System.Convert]::ToBase64String(
        $serverCertificates[0].RawData)
    if (
        $null -eq $selectedServerCertificate `
        -or $selectedServerCertificate.HasPrivateKey `
        -or $selectedServerCertificateBytes -ne $activeServerCertificateBytes)
    {
        throw "Inputs"
    }
    $serverIssuer = [System.Convert]::ToBase64String(
        $serverCertificates[0].IssuerName.RawData)
    $signingRoots = @(
        $personalStore.Certificates |
            Where-Object {
                $subject = [System.Convert]::ToBase64String($_.SubjectName.RawData)
                $issuer = [System.Convert]::ToBase64String($_.IssuerName.RawData)
                $subject -eq $serverIssuer `
                    -and $subject -eq $issuer `
                    -and $_.HasPrivateKey
            }
    )
    if ($signingRoots.Count -ne 1)
    {
        throw "Inputs"
    }
    $signingRootBytes = [System.Convert]::ToBase64String($signingRoots[0].RawData)
    $trustedRoots = @(
        $rootStore.Certificates |
            Where-Object {
                [System.Convert]::ToBase64String($_.RawData) -eq $signingRootBytes
            }
    )
    if ($trustedRoots.Count -ne 1)
    {
        throw "Inputs"
    }

    $allPublicationPaths = @(
        $desktopConfiguration,
        $sourceProfile,
        $trustedServerCertificatePath,
        $enrollment,
        $authorizationPolicy,
        $certificate,
        $privateKey,
        $profile)
    if (@($allPublicationPaths | Sort-Object -Unique).Count -ne 8)
    {
        throw "Targets"
    }
    foreach ($target in @($certificate, $privateKey, $profile))
    {
        if (-not (Test-PathWithin -Parent $provisioningRoot -Candidate $target))
        {
            throw "Targets"
        }
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent -PathType Container))
        {
            throw "Targets"
        }
        if (Test-Path -LiteralPath $target -PathType Container)
        {
            throw "Targets"
        }
        Assert-NoReparsePoint -Path $target
    }

    $journals = @(
        Get-ChildItem -LiteralPath $provisioningRoot `
            -Filter ".hase-python-provisioning-*.journal.json*" `
            -File -ErrorAction Stop)
    $artifacts = @()
    foreach ($target in @(
        $certificate,
        $privateKey,
        $profile,
        $enrollment,
        $authorizationPolicy))
    {
        $artifacts += @(Get-ChildItem -LiteralPath (
            Split-Path -Parent $target) -File -ErrorAction Stop |
            Where-Object {
                $_.FullName.StartsWith(
                    $target + ".stage-",
                    [System.StringComparison]::OrdinalIgnoreCase) `
                -or $_.FullName.StartsWith(
                    $target + ".backup-",
                    [System.StringComparison]::OrdinalIgnoreCase)
            })
    }
    if ($journals.Count -ne 0 -or $artifacts.Count -ne 0)
    {
        throw "Transaction"
    }

    $rollbackParent = Split-Path -Parent $script:rollbackDirectoryPath
    if (
        [string]::IsNullOrWhiteSpace($rollbackParent) `
        -or -not (Test-Path -LiteralPath $rollbackParent -PathType Container) `
        -or (Test-Path -LiteralPath $script:rollbackDirectoryPath) `
        -or (Test-PathWithin `
            -Parent $repositoryRoot `
            -Candidate $script:rollbackDirectoryPath) `
        -or (Test-PathWithin `
            -Parent $provisioningRoot `
            -Candidate $script:rollbackDirectoryPath))
    {
        throw "Rollback"
    }
    Assert-NoReparsePoint -Path $rollbackParent

    [System.IO.Directory]::CreateDirectory($script:rollbackDirectoryPath) |
        Out-Null
    $rollbackCreated = $true
    Set-PrivateDirectorySecurity -Path $script:rollbackDirectoryPath

    $entries = @(
        Get-FileState "sourceProfile" $sourceProfile "source-profile.original"
        Get-FileState "enrollment" $enrollment "enrollment.original"
        Get-FileState "authorizationPolicy" $authorizationPolicy (
            "authorization-policy.original")
        Get-FileState "certificate" $certificate "certificate.original"
        Get-FileState "privateKey" $privateKey "private-key.original"
        Get-FileState "profile" $profile "profile.original"
    )
    $authorizationHash = (
        Get-FileHash -LiteralPath $authorizationPolicy -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $replacementRequired = @(
        $entries |
            Where-Object {
                $_.name -in @("certificate", "privateKey", "profile") `
                    -and $_.existed
            }
    ).Count -ne 0

    $sourceProfileDocument = [ordered]@{
        formatVersion = 1
        address = $clientAddress
        clientCertificate = [ordered]@{
            certificateChainPath = $certificate
            privateKeyPath = $privateKey
        }
        trustedServerCertificate = [ordered]@{
            certificatePath = $trustedServerCertificatePath
        }
    }
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText(
        $sourceProfile,
        ($sourceProfileDocument | ConvertTo-Json -Depth 8),
        $utf8)
    $sourceProfileCreated = $true
    Set-PrivateFileSecurity -Path $sourceProfile

    $operatorInputs = [ordered]@{
        formatVersion = 1
        signingRootThumbprint = $signingRoots[0].Thumbprint.ToUpperInvariant()
        trustPolicyId = $trustPolicyIds[0]
        sourceProfilePath = $sourceProfile
        provisioningDirectory = $provisioningRoot
        certificatePath = $certificate
        privateKeyPath = $privateKey
        profilePath = $profile
        enrollmentPath = $enrollment
        authorizationPolicyPath = $authorizationPolicy
        expectedAuthorizationPolicySha256 = $authorizationHash
        validityDays = $ValidityDays
        allowReplacement = $replacementRequired
    }
    $manifest = [ordered]@{
        formatVersion = 1
        capturedUtc = [System.DateTimeOffset]::UtcNow.ToString("O")
        repositoryHead = $head
        entries = $entries
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $script:rollbackDirectoryPath "operator-inputs.json"),
        ($operatorInputs | ConvertTo-Json -Depth 8),
        $utf8)
    [System.IO.File]::WriteAllText(
        (Join-Path $script:rollbackDirectoryPath "rollback-manifest.json"),
        ($manifest | ConvertTo-Json -Depth 8),
        $utf8)

    foreach ($file in Get-ChildItem -LiteralPath $script:rollbackDirectoryPath -File)
    {
        Set-PrivateFileSecurity -Path $file.FullName
        $acl = Get-Acl -LiteralPath $file.FullName
        if (-not $acl.AreAccessRulesProtected)
        {
            throw "Rollback"
        }
    }

    Write-Host "Repository baseline ready       : True"
    Write-Host "Runtime processes stopped       : True"
    Write-Host "Provisioning readiness ready    : True"
    Write-Host "Authoritative inputs ready      : True"
    Write-Host "Public server certificate ready : True"
    Write-Host "Python profile template ready   : True"
    Write-Host "Publication targets ready       : True"
    Write-Host "Transaction directory clean     : True"
    Write-Host "Rollback content captured       : True"
    Write-Host "Rollback security captured      : True"
    Write-Host "Physical provisioning ready     : True"
}
catch
{
    if ($sourceProfileCreated -and (Test-Path -LiteralPath $sourceProfile))
    {
        [System.IO.File]::Delete($sourceProfile)
    }
    if ($rollbackCreated -and (Test-Path -LiteralPath $script:rollbackDirectoryPath))
    {
        [System.IO.Directory]::Delete($script:rollbackDirectoryPath, $true)
    }
    Write-Error "Python physical provisioning preflight failed."
    exit 1
}
finally
{
    if ($null -ne $personalStore)
    {
        $personalStore.Dispose()
    }
    if ($null -ne $rootStore)
    {
        $rootStore.Dispose()
    }
    if ($null -ne $selectedServerCertificate)
    {
        $selectedServerCertificate.Dispose()
    }
}
