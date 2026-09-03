[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $DesktopProfilePath,
    [Parameter(Mandatory = $true)] [string] $TransferArchivePath,
    [Parameter(Mandatory = $true)] [string] $MiniPcCredentialDirectory,
    [Parameter(Mandatory = $true)] [string] $MiniPcCertificatePath,
    [Parameter(Mandatory = $true)] [string] $MiniPcPrivateKeyPath,
    [Parameter(Mandatory = $true)] [string] $MiniPcProfilePath,
    [Parameter(Mandatory = $true)] [string] $MiniPcTrustedServerCertificatePath,
    [Parameter(Mandatory = $true)] [string] $TargetRegistryPath,
    [Parameter(Mandatory = $true)] [string] $RollbackDirectory,
    [Parameter(Mandatory = $true)] [string] $ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$phase = "preflight"

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

function Write-Json([string] $Path, $Value)
{
    $utf8 = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText(
        $Path, ($Value | ConvertTo-Json -Depth 12), $utf8)
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

try
{
    if ($env:OS -ne "Windows_NT" -or $env:COMPUTERNAME -cne $ExpectedComputer)
    { throw "machine" }
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $packageDirectory "..\.."))
    $python = Join-Path $packageDirectory ".venv\Scripts\python.exe"
    if (-not (Test-Path -LiteralPath $python -PathType Leaf)) { throw "python" }
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $desktop = Resolve-AbsolutePath $DesktopProfilePath
    $archivePath = Resolve-AbsolutePath $TransferArchivePath
    $credentialDirectory = Resolve-AbsolutePath $MiniPcCredentialDirectory
    $certificate = Resolve-AbsolutePath $MiniPcCertificatePath
    $privateKey = Resolve-AbsolutePath $MiniPcPrivateKeyPath
    $profile = Resolve-AbsolutePath $MiniPcProfilePath
    $trustedServer = Resolve-AbsolutePath $MiniPcTrustedServerCertificatePath
    $registry = Resolve-AbsolutePath $TargetRegistryPath
    $script:rollback = Resolve-AbsolutePath $RollbackDirectory
    foreach ($input in @($desktop, $archivePath, $trustedServer))
    {
        if (-not (Test-Path -LiteralPath $input -PathType Leaf)) { throw "input" }
    }
    foreach ($target in @($credentialDirectory, $certificate, $privateKey,
            $profile, $registry, $script:rollback))
    {
        if (Test-Path -LiteralPath $target) { throw "target" }
        if (Test-Within $repositoryRoot $target) { throw "repository-custody" }
    }
    foreach ($leaf in @($certificate, $privateKey, $profile))
    {
        if (-not (Test-Within $credentialDirectory $leaf))
        { throw "credential-custody" }
    }
    if ((Test-Within $credentialDirectory $registry) `
        -or (Test-Within $credentialDirectory $script:rollback))
    { throw "external-custody" }
    foreach ($parent in @(
            (Split-Path -Parent $credentialDirectory),
            (Split-Path -Parent $registry),
            (Split-Path -Parent $script:rollback)))
    {
        if (-not (Test-Path -LiteralPath $parent -PathType Container))
        { throw "parent" }
    }

    Set-PrivateFile $archivePath
    if (-not (Get-Acl -LiteralPath $archivePath).AreAccessRulesProtected)
    { throw "archive-acl" }
    $archiveHash = (Get-FileHash `
        -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try
    {
        $expectedNames = @(
            "client-certificate.pem", "private-key.pem",
            "runtime-host-profile.json", "transfer-manifest.json")
        $entries = @($archive.Entries)
        if ($entries.Count -ne 4 `
            -or @($entries | Where-Object { $_.FullName -cne $_.Name }).Count -ne 0 `
            -or @(Compare-Object ($expectedNames | Sort-Object) `
                ($entries.Name | Sort-Object)).Count -ne 0)
        { throw "archive-shape" }
        $manifestEntry = $entries | Where-Object Name -ceq "transfer-manifest.json"
        $reader = [IO.StreamReader]::new(
            $manifestEntry.Open(), [Text.UTF8Encoding]::new($false, $true))
        try { $manifestText = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $manifest = $manifestText | ConvertFrom-Json
        if ($manifest.schemaVersion -ne 1 `
            -or $manifest.purpose -cne "hase-laptop-python-minipc-credential-transfer" `
            -or $manifest.principalId -cne "hase-laptop-python-minipc" `
            -or @($manifest.packageFiles).Count -ne 3 `
            -or -not [string]::Equals([string]$manifest.destination.certificatePath,
                $certificate, [StringComparison]::OrdinalIgnoreCase) `
            -or -not [string]::Equals([string]$manifest.destination.privateKeyPath,
                $privateKey, [StringComparison]::OrdinalIgnoreCase) `
            -or -not [string]::Equals([string]$manifest.destination.profilePath,
                $profile, [StringComparison]::OrdinalIgnoreCase) `
            -or -not [string]::Equals(
                [string]$manifest.destination.trustedServerCertificatePath,
                $trustedServer, [StringComparison]::OrdinalIgnoreCase))
        { throw "manifest" }
        foreach ($record in $manifest.packageFiles)
        {
            $entry = $entries | Where-Object Name -ceq ([string]$record.name)
            if ($null -eq $entry -or [string]$record.sha256 -notmatch '^[0-9a-f]{64}$')
            { throw "manifest" }
            $stream = $entry.Open();$sha = [Security.Cryptography.SHA256]::Create()
            try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace("-","").ToLowerInvariant()}
            finally{$sha.Dispose();$stream.Dispose()}
            if ($actual -cne [string]$record.sha256) { throw "archive-hash" }
        }

        [IO.Directory]::CreateDirectory($script:rollback) | Out-Null
        Set-PrivateDirectory $script:rollback
        $stage = Join-Path $script:rollback "credential.stage"
        [IO.Directory]::CreateDirectory($stage) | Out-Null
        Set-PrivateDirectory $stage
        foreach ($name in $expectedNames)
        {
            $entry = $entries | Where-Object Name -ceq $name
            $target = Join-Path $stage $name
            $inputStream = $entry.Open();$outputStream=[IO.File]::Create($target)
            try{$inputStream.CopyTo($outputStream)}
            finally{$outputStream.Dispose();$inputStream.Dispose()}
            Set-PrivateFile $target
        }
    }
    finally { $archive.Dispose() }

    $stageCertificate = Join-Path $stage "client-certificate.pem"
    $stagePrivateKey = Join-Path $stage "private-key.pem"
    $stageProfile = Join-Path $stage "runtime-host-profile.json"
    & $python -c "import ssl,sys;c=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT);c.load_cert_chain(sys.argv[1],sys.argv[2])" `
        $stageCertificate $stagePrivateKey
    if ($LASTEXITCODE -ne 0) { throw "credential-pair" }
    $desktopHash = (Get-FileHash `
        -LiteralPath $desktop -Algorithm SHA256).Hash.ToLowerInvariant()
    $desktopSddl = (Get-Acl -LiteralPath $desktop).Sddl
    & $python -c "from hase import load_runtime_host_profile;import sys;load_runtime_host_profile(sys.argv[1])" $desktop
    if ($LASTEXITCODE -ne 0) { throw "desktop-profile" }
    $desktopDocument = Get-Content -LiteralPath $desktop -Raw | ConvertFrom-Json
    $desktopTrustedServer = Resolve-AbsolutePath `
        ([string]$desktopDocument.trustedServerCertificate.certificatePath)
    if ((Get-FileHash -LiteralPath $desktopTrustedServer -Algorithm SHA256).Hash `
        -ceq (Get-FileHash -LiteralPath $trustedServer -Algorithm SHA256).Hash)
    { throw "server-certificates-not-distinct" }
    $plan = [ordered]@{
        schemaVersion = 1
        purpose = "hase-laptop-minipc-python-credential-import"
        repositoryHead = $head
        archivePath = $archivePath
        archiveSha256 = $archiveHash
        desktopProfilePath = $desktop
        desktopProfileSha256 = $desktopHash
        desktopProfileSddl = $desktopSddl
        credentialDirectory = $credentialDirectory
        certificatePath = $certificate
        privateKeyPath = $privateKey
        profilePath = $profile
        targetRegistryPath = $registry
        trustedServerCertificatePath = $trustedServer
    }
    Write-Json (Join-Path $script:rollback "import-plan.json") $plan
    Write-Json (Join-Path $script:rollback "transfer-manifest.json") $manifest
    foreach ($file in Get-ChildItem -LiteralPath $script:rollback -File)
    { Set-PrivateFile $file.FullName }
    $journal = Join-Path $script:rollback "import-journal.json"
    $state = [ordered]@{schemaVersion=1;purpose="hase-laptop-minipc-python-import";status="validated"}
    Write-Json $journal $state;Set-PrivateFile $journal

    Remove-Item -LiteralPath (Join-Path $stage "transfer-manifest.json") -Force
    Move-Item -LiteralPath $stage -Destination $credentialDirectory
    $phase = "credential-published";$state.status=$phase;Write-Json $journal $state
    & $python -c "from hase import load_runtime_host_profile;import sys;load_runtime_host_profile(sys.argv[1])" $profile
    if ($LASTEXITCODE -ne 0) { throw "profile" }
    $registryDocument = [ordered]@{
        formatVersion = 1
        targets = @(
            [ordered]@{targetId="desktop-runtime-host";displayName="Desktop Runtime Host";profilePath=$desktop},
            [ordered]@{targetId="minipc-runtime-host";displayName="MiniPC Runtime Host";profilePath=$profile})
    }
    Write-Json $registry $registryDocument;Set-PrivateFile $registry
    $phase = "registry-published";$state.status=$phase;Write-Json $journal $state
    & $python -c "from hase import load_automation_target_registry;import sys;r=load_automation_target_registry(sys.argv[1]);assert len(r.targets)==2" $registry
    if ($LASTEXITCODE -ne 0) { throw "registry" }
    if ((Get-FileHash -LiteralPath $desktop -Algorithm SHA256).Hash.ToLowerInvariant() `
        -cne $desktopHash -or (Get-Acl -LiteralPath $desktop).Sddl -cne $desktopSddl)
    { throw "desktop-profile" }
    Remove-Item -LiteralPath $archivePath -Force
    $phase="committed";$state.status=$phase;Write-Json $journal $state
    Remove-Item -LiteralPath $journal -Force

    Write-Host "Repository baseline ready       : True"
    Write-Host "Transfer archive verified       : True"
    Write-Host "Manifest destinations exact     : True"
    Write-Host "Certificate key pair valid      : True"
    Write-Host "Private MiniPC custody published: True"
    Write-Host "Desktop profile preserved       : True"
    Write-Host "Two-target registry published   : True"
    Write-Host "Both profiles valid             : True"
    Write-Host "Incoming archive removed        : True"
    Write-Host "Rollback evidence retained      : True"
    Write-Host "Connection attempted            : False"
    Write-Host "Laptop credential import ready  : True"
}
catch
{
    Write-Error "Laptop MiniPC Python credential import failed at phase '$phase'; explicit recovery may be required."
    exit 1
}
