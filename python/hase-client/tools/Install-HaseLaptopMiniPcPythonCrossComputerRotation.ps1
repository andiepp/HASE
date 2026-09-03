[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TransferArchivePath,

    [Parameter(Mandatory = $true)]
    [string] $InstalledProfilePath,

    [Parameter(Mandatory = $true)]
    [string] $CutoverDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-HaseAbsolutePath([string] $Path)
{
    if ([string]::IsNullOrWhiteSpace($Path) -or
        $Path -cne $Path.Trim() -or
        $Path -notmatch '^[A-Za-z]:[\\/]')
    {
        throw "A fully qualified Windows path was required."
    }

    return [IO.Path]::GetFullPath($Path)
}

function Test-HaseReparsePointInExistingChain([string] $Path)
{
    $candidate = [IO.Path]::GetFullPath($Path)

    while (-not [string]::IsNullOrEmpty($candidate))
    {
        if (Test-Path -LiteralPath $candidate)
        {
            $item = Get-Item -LiteralPath $candidate -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            {
                return $true
            }
        }

        $parent = Split-Path -Parent $candidate
        if ($parent -ceq $candidate)
        {
            break
        }

        $candidate = $parent
    }

    return $false
}

function Set-HasePrivateDirectory([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($user)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule(
        [Security.AccessControl.FileSystemAccessRule]::new(
            $user,
            "FullControl",
            "ContainerInherit,ObjectInherit",
            "None",
            "Allow"))
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Set-HasePrivateFile([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = [Security.AccessControl.FileSecurity]::new()
    $acl.SetOwner($user)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule(
        [Security.AccessControl.FileSystemAccessRule]::new(
            $user,
            "FullControl",
            "Allow"))
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Get-HaseSha256([byte[]] $Bytes)
{
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try
    {
        $hash = $sha256.ComputeHash($Bytes)
        try
        {
            return [BitConverter]::ToString($hash).
                Replace("-", "").
                ToLowerInvariant()
        }
        finally
        {
            [Array]::Clear($hash, 0, $hash.Length)
        }
    }
    finally
    {
        $sha256.Dispose()
    }
}

function Get-HaseCredentialId([byte[]] $CertificateBytes)
{
    $certificate =
        [Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $CertificateBytes)
    try
    {
        return "x509-sha256:" + (Get-HaseSha256 $certificate.RawData)
    }
    finally
    {
        $certificate.Dispose()
    }
}

function Write-HaseUtf8Json([string] $Path, [object] $Value)
{
    $json = $Value | ConvertTo-Json -Depth 8
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    try
    {
        [IO.File]::WriteAllBytes($Path, $bytes)
    }
    finally
    {
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}

function Restore-HaseInstalledFiles(
    [object[]] $Files,
    [string] $RollbackDirectory)
{
    foreach ($file in $Files)
    {
        $backupPath = Join-Path $RollbackDirectory $file.Name
        if (Test-Path -LiteralPath $backupPath -PathType Leaf)
        {
            Copy-Item -LiteralPath $backupPath -Destination $file.Path -Force
            $restoredAccessSddl = (Get-Acl -LiteralPath $file.Path).
                GetSecurityDescriptorSddlForm(
                    [Security.AccessControl.AccessControlSections]::Access)
            if ($restoredAccessSddl -cne $file.OriginalAccessSddl)
            {
                throw "Installed ACL changed during rollback."
            }
        }
    }
}

$phase = "preflight"
$payloads = @{}
$installedFiles = @()
$rollbackDirectory = $null
$installationStarted = $false

try
{
    if ($env:OS -cne "Windows_NT" -or $env:COMPUTERNAME -cne $ExpectedComputer)
    {
        throw "The cutover must run on $ExpectedComputer."
    }

    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repositoryRoot = [IO.Path]::GetFullPath(
        (Join-Path $toolDirectory "..\..\.."))
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()

    if ($head -cne $origin -or
        @(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    {
        throw "The repository baseline was not synchronized and clean."
    }

    if (@(Get-Process -Name "Hase.DesktopHost.App" `
            -ErrorAction SilentlyContinue).Count -ne 0 -or
        @(Get-Process -Name "Hase.Client.Wpf.App" `
            -ErrorAction SilentlyContinue).Count -ne 0)
    {
        throw "Runtime Host and Client processes must be stopped."
    }

    $incomingArchive = Resolve-HaseAbsolutePath $TransferArchivePath
    $profilePath = Resolve-HaseAbsolutePath $InstalledProfilePath
    $custodyPath = Resolve-HaseAbsolutePath $CutoverDirectory

    if (-not (Test-Path -LiteralPath $incomingArchive -PathType Leaf) -or
        -not (Test-Path -LiteralPath $profilePath -PathType Leaf))
    {
        throw "A required cutover input was absent."
    }

    if (Test-Path -LiteralPath $custodyPath)
    {
        throw "The cutover custody directory already exists."
    }

    $incomingReparse = Test-HaseReparsePointInExistingChain $incomingArchive
    $profileReparse = Test-HaseReparsePointInExistingChain $profilePath
    $parentReparse = Test-HaseReparsePointInExistingChain `
        (Split-Path -Parent $custodyPath)
    if ($incomingReparse -or $profileReparse -or $parentReparse)
    {
        throw "Reparse-point custody was rejected."
    }

    $repositoryPrefix = $repositoryRoot.TrimEnd("\") + "\"
    if ($custodyPath.StartsWith(
            $repositoryPrefix,
            [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Credential custody must remain outside the repository."
    }

    $currentProfile = Get-Content -LiteralPath $profilePath -Raw |
        ConvertFrom-Json -ErrorAction Stop
    $certificatePath = Resolve-HaseAbsolutePath `
        ([string]$currentProfile.clientCertificate.certificateChainPath)
    $privateKeyPath = Resolve-HaseAbsolutePath `
        ([string]$currentProfile.clientCertificate.privateKeyPath)

    foreach ($path in @($certificatePath, $privateKeyPath))
    {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Test-HaseReparsePointInExistingChain $path))
        {
            throw "Installed credential custody was incomplete or indirect."
        }
    }

    $phase = "archive-validation"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($incomingArchive)
    try
    {
        $expectedNames = @(
            "client-certificate.pem",
            "private-key.pem",
            "runtime-host-profile.json",
            "transfer-manifest.json") | Sort-Object
        $actualNames = @($archive.Entries | ForEach-Object {
            $_.FullName
        })

        if ($actualNames.Count -ne 4 -or
            @($actualNames | Sort-Object -Unique).Count -ne 4 -or
            @(Compare-Object `
                -ReferenceObject $expectedNames `
                -DifferenceObject ($actualNames | Sort-Object)).Count -ne 0)
        {
            throw "The replacement archive shape was invalid."
        }

        foreach ($entry in $archive.Entries)
        {
            $memory = [IO.MemoryStream]::new()
            try
            {
                $input = $entry.Open()
                try
                {
                    $input.CopyTo($memory)
                }
                finally
                {
                    $input.Dispose()
                }
                $payloads[$entry.FullName] = $memory.ToArray()
            }
            finally
            {
                $memory.Dispose()
            }
        }
    }
    finally
    {
        $archive.Dispose()
    }

    $manifestText = [Text.Encoding]::UTF8.GetString(
        [byte[]]$payloads["transfer-manifest.json"])
    $manifest = $manifestText | ConvertFrom-Json -ErrorAction Stop

    if ([int]$manifest.schemaVersion -ne 1 -or
        [string]$manifest.purpose -cne `
            "hase-laptop-minipc-python-cross-computer-rotation-package" -or
        [string]$manifest.principalId -cne "hase-laptop-python-minipc")
    {
        throw "The replacement manifest identity was invalid."
    }

    $manifestFiles = @($manifest.files)
    if ($manifestFiles.Count -ne 3)
    {
        throw "The replacement manifest file set was invalid."
    }

    foreach ($name in @(
        "client-certificate.pem",
        "private-key.pem",
        "runtime-host-profile.json"))
    {
        $manifestMatch = @($manifestFiles | Where-Object {
            [string]$_.name -ceq $name
        })
        $payloadHash = Get-HaseSha256 ([byte[]]$payloads[$name])
        if ($manifestMatch.Count -ne 1 -or
            [string]$manifestMatch[0].sha256 -cne $payloadHash)
        {
            throw "A replacement payload hash was invalid."
        }
    }

    $oldCertificateBytes = [IO.File]::ReadAllBytes($certificatePath)
    try
    {
        $oldCredentialId = Get-HaseCredentialId $oldCertificateBytes
    }
    finally
    {
        [Array]::Clear(
            $oldCertificateBytes, 0, $oldCertificateBytes.Length)
    }
    $newCredentialId = Get-HaseCredentialId `
        ([byte[]]$payloads["client-certificate.pem"])

    if ($oldCredentialId -cne [string]$manifest.currentCredentialId -or
        $newCredentialId -cne [string]$manifest.replacementCredentialId -or
        $oldCredentialId -ceq $newCredentialId)
    {
        throw "The credential transition identity was invalid."
    }

    $replacementProfileText = [Text.Encoding]::UTF8.GetString(
        [byte[]]$payloads["runtime-host-profile.json"])
    $replacementProfile = $replacementProfileText |
        ConvertFrom-Json -ErrorAction Stop
    $replacementCertificatePath = Resolve-HaseAbsolutePath `
        ([string]$replacementProfile.clientCertificate.certificateChainPath)
    $replacementPrivateKeyPath = Resolve-HaseAbsolutePath `
        ([string]$replacementProfile.clientCertificate.privateKeyPath)
    if ($replacementCertificatePath -cne $certificatePath -or
        $replacementPrivateKeyPath -cne $privateKeyPath)
    {
        throw "The replacement profile changed installed custody paths."
    }

    $phase = "preparation"
    [IO.Directory]::CreateDirectory($custodyPath) | Out-Null
    Set-HasePrivateDirectory $custodyPath
    $rollbackDirectory = Join-Path $custodyPath "rollback"
    $stageDirectory = Join-Path $custodyPath "stage"
    [IO.Directory]::CreateDirectory($rollbackDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($stageDirectory) | Out-Null

    $protectedArchive = Join-Path $custodyPath "replacement-transfer.zip"
    Copy-Item -LiteralPath $incomingArchive -Destination $protectedArchive
    Set-HasePrivateFile $protectedArchive
    if ((Get-FileHash -LiteralPath $incomingArchive -Algorithm SHA256).Hash `
        -cne
        (Get-FileHash -LiteralPath $protectedArchive -Algorithm SHA256).Hash)
    {
        throw "The protected archive import was not byte-exact."
    }

    $installedFiles = @(
        [pscustomobject]@{
            Name = "client-certificate.pem"
            Path = $certificatePath
            OriginalAccessSddl = (Get-Acl -LiteralPath $certificatePath).
                GetSecurityDescriptorSddlForm(
                    [Security.AccessControl.AccessControlSections]::Access)
        },
        [pscustomobject]@{
            Name = "private-key.pem"
            Path = $privateKeyPath
            OriginalAccessSddl = (Get-Acl -LiteralPath $privateKeyPath).
                GetSecurityDescriptorSddlForm(
                    [Security.AccessControl.AccessControlSections]::Access)
        },
        [pscustomobject]@{
            Name = "runtime-host-profile.json"
            Path = $profilePath
            OriginalAccessSddl = (Get-Acl -LiteralPath $profilePath).
                GetSecurityDescriptorSddlForm(
                    [Security.AccessControl.AccessControlSections]::Access)
        })

    foreach ($file in $installedFiles)
    {
        Copy-Item -LiteralPath $file.Path `
            -Destination (Join-Path $rollbackDirectory $file.Name)
        [IO.File]::WriteAllBytes(
            (Join-Path $stageDirectory $file.Name),
            [byte[]]$payloads[$file.Name])
    }

    $journalPath = Join-Path $custodyPath `
        "laptop-cutover.transaction.json"
    $journal = [ordered]@{
        schemaVersion = 1
        purpose = "hase-laptop-minipc-python-cross-computer-rotation-cutover"
        transactionId = [string]$manifest.transactionId
        phase = "prepared"
        currentCredentialId = $oldCredentialId
        replacementCredentialId = $newCredentialId
        profilePath = $profilePath
        certificatePath = $certificatePath
        privateKeyPath = $privateKeyPath
        archiveSha256 = (Get-FileHash `
            -LiteralPath $protectedArchive `
            -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    Write-HaseUtf8Json $journalPath $journal
    Set-HasePrivateFile $journalPath

    $phase = "installation"
    $installationStarted = $true
    foreach ($file in $installedFiles)
    {
        Copy-Item -LiteralPath (Join-Path $stageDirectory $file.Name) `
            -Destination $file.Path -Force
        $installedAccessSddl = (Get-Acl -LiteralPath $file.Path).
            GetSecurityDescriptorSddlForm(
                [Security.AccessControl.AccessControlSections]::Access)
        if ($installedAccessSddl -cne $file.OriginalAccessSddl)
        {
            throw "Installed ACL changed during replacement."
        }
    }

    $phase = "installed-verification"
    foreach ($file in $installedFiles)
    {
        $installedHash = (Get-FileHash `
            -LiteralPath $file.Path `
            -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = Get-HaseSha256 ([byte[]]$payloads[$file.Name])
        if ($installedHash -cne $expectedHash)
        {
            throw "Installed replacement verification failed."
        }
    }

    $journal.phase = "replacement-installed"
    Write-HaseUtf8Json $journalPath $journal
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force
    Remove-Item -LiteralPath $incomingArchive -Force

    Write-Host "Archive shape valid          : True"
    Write-Host "Manifest and hashes valid    : True"
    Write-Host "Credential transition exact  : True"
    Write-Host "Protected import byte-exact  : True"
    Write-Host "Replacement installed        : True"
    Write-Host "Rollback retained            : True"
    Write-Host "Downloads archive removed    : True"
    Write-Host "MiniPC overlap changed       : False"
    Write-Host "Laptop cutover ready         : True"
}
catch
{
    $primaryFailureType = $_.Exception.GetType().FullName
    $primaryFailureHResult = $_.Exception.HResult

    if ($installationStarted -and
        $null -ne $rollbackDirectory -and
        $installedFiles.Count -ne 0)
    {
        try
        {
            Restore-HaseInstalledFiles `
                $installedFiles $rollbackDirectory
        }
        catch
        {
            $rollbackFailureType = $_.Exception.GetType().FullName
            $rollbackFailureHResult = $_.Exception.HResult
            Write-Error `
                "Laptop cutover rollback requires operator recovery. Primary: $primaryFailureType/$primaryFailureHResult; rollback: $rollbackFailureType/$rollbackFailureHResult."
            exit 2
        }
    }

    Write-Error `
        "Laptop cutover failed at phase '$phase'. Primary: $primaryFailureType/$primaryFailureHResult."
    exit 1
}
finally
{
    foreach ($payload in $payloads.Values)
    {
        if ($payload -is [byte[]])
        {
            [Array]::Clear($payload, 0, $payload.Length)
        }
    }
}
