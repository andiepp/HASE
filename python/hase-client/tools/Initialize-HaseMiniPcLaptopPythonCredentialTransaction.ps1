[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $MiniPcConfigurationPath,
    [Parameter(Mandatory = $true)] [string] $ApplicationProfilePath,
    [Parameter(Mandatory = $true)] [string] $TrustedServerCertificatePath,
    [Parameter(Mandatory = $true)] [string] $AuthorityManifestPath,
    [Parameter(Mandatory = $true)] [string] $AuthorityRollbackEvidencePath,
    [Parameter(Mandatory = $true)] [string] $StagingDirectory,
    [Parameter(Mandatory = $true)] [string] $CertificatePath,
    [Parameter(Mandatory = $true)] [string] $PrivateKeyPath,
    [Parameter(Mandatory = $true)] [string] $ProfilePath,
    [Parameter(Mandatory = $true)] [string] $ProfileTemplatePath,
    [Parameter(Mandatory = $true)] [string] $TransferArchivePath,
    [Parameter(Mandatory = $true)] [string] $RollbackDirectory,
    [Parameter(Mandatory = $true)] [ValidateRange(1, 90)] [int] $ValidityDays
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$rollbackCreated = $false
$templateCreated = $false
$laptopPrincipal = "hase-laptop-python-minipc"
$laptopPermissions = @(
    "runtime-host.snapshot.read",
    "property.authoritative.read")

function Resolve-AbsolutePath([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]')) { throw "path" }
    return [System.IO.Path]::GetFullPath($Value)
}

function Test-Within([string] $Parent, [string] $Candidate)
{
    $prefix = [System.IO.Path]::GetFullPath($Parent).TrimEnd("\") + "\"
    return [System.IO.Path]::GetFullPath($Candidate).StartsWith(
        $prefix, [System.StringComparison]::OrdinalIgnoreCase)
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
    if ($env:OS -ne "Windows_NT" -or $env:COMPUTERNAME -cne "LABC")
    { throw "machine" }
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $packageDirectory "..\.."))
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $configurationPath = Resolve-AbsolutePath $MiniPcConfigurationPath
    $applicationPath = Resolve-AbsolutePath $ApplicationProfilePath
    $trustedServerPath = Resolve-AbsolutePath $TrustedServerCertificatePath
    $manifestPath = Resolve-AbsolutePath $AuthorityManifestPath
    $authorityRollbackPath = Resolve-AbsolutePath $AuthorityRollbackEvidencePath
    $staging = Resolve-AbsolutePath $StagingDirectory
    $certificate = Resolve-AbsolutePath $CertificatePath
    $privateKey = Resolve-AbsolutePath $PrivateKeyPath
    $profile = Resolve-AbsolutePath $ProfilePath
    $template = Resolve-AbsolutePath $ProfileTemplatePath
    $transfer = Resolve-AbsolutePath $TransferArchivePath
    $script:rollback = Resolve-AbsolutePath $RollbackDirectory

    foreach ($input in @($configurationPath, $applicationPath, $trustedServerPath,
            $manifestPath, $authorityRollbackPath))
    {
        if (-not (Test-Path -LiteralPath $input -PathType Leaf)) { throw "input" }
    }
    foreach ($target in @($staging, $certificate, $privateKey, $profile,
            $template, $transfer, $script:rollback))
    {
        if (Test-Path -LiteralPath $target) { throw "target" }
    }
    foreach ($leaf in @($certificate, $privateKey, $profile))
    {
        if (-not (Test-Within $staging $leaf)) { throw "staging-custody" }
    }
    foreach ($external in @($template, $transfer, $script:rollback))
    {
        if ((Test-Within $staging $external) -or (Test-Within $repositoryRoot $external))
        { throw "external-custody" }
        if (-not (Test-Path -LiteralPath (Split-Path -Parent $external) -PathType Container))
        { throw "parent" }
    }
    if (-not (Test-Path -LiteralPath (Split-Path -Parent $staging) -PathType Container))
    { throw "parent" }

    & (Join-Path $toolDirectory "Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1") `
        -MiniPcConfigurationPath $configurationPath `
        -ApplicationProfilePath $applicationPath `
        -AuthorityManifestPath $manifestPath `
        -AuthorityRollbackEvidencePath $authorityRollbackPath `
        -StagingDirectory $staging `
        -CertificatePath $certificate `
        -PrivateKeyPath $privateKey `
        -ProfilePath $profile `
        -TransferArchivePath $transfer `
        -RollbackDirectory $script:rollback *> $null
    if ($LASTEXITCODE -ne 0) { throw "readiness" }

    $configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
    $application = Get-Content -LiteralPath $applicationPath -Raw | ConvertFrom-Json
    $enrollmentPath = Resolve-AbsolutePath ([string]$configuration.clientEnrollmentFilePath)
    $authorizationPath = Resolve-AbsolutePath ([string]$application.authorizationPolicyFilePath)
    $enrollment = Get-Content -LiteralPath $enrollmentPath -Raw | ConvertFrom-Json
    $authorization = Get-Content -LiteralPath $authorizationPath -Raw | ConvertFrom-Json
    $authority = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $authorityRollback = Get-Content -LiteralPath $authorityRollbackPath -Raw | ConvertFrom-Json
    if ($authority.purpose -cne "hase-minipc-python-client-authority" `
        -or [string]$authority.thumbprint -cne [string]$authorityRollback.thumbprint `
        -or [string]$authority.certificateSha256 -cne [string]$authorityRollback.certificateSha256)
    { throw "authority" }

    $serverCertificate = Get-PfxCertificate -FilePath $trustedServerPath
    $activeServer = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Thumbprint -ieq [string]$configuration.serverCertificate.thumbprint })
    if ($activeServer.Count -ne 1 -or $serverCertificate.HasPrivateKey `
        -or [Convert]::ToBase64String($serverCertificate.RawData) -cne `
            [Convert]::ToBase64String($activeServer[0].RawData))
    { throw "server-certificate" }

    $ip = $null
    if (-not [Net.IPAddress]::TryParse([string]$configuration.binding.address, [ref]$ip))
    { throw "binding" }
    $hostText = $ip.ToString()
    if ($ip.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetworkV6)
    { $hostText = "[$hostText]" }
    $address = "https://${hostText}:$([int]$configuration.binding.port)"

    [IO.Directory]::CreateDirectory($script:rollback) | Out-Null
    $rollbackCreated = $true
    Set-PrivateDirectory $script:rollback
    $utf8 = [Text.UTF8Encoding]::new($false)
    $sourceProfile = [ordered]@{
        formatVersion = 1
        address = $address
        clientCertificate = [ordered]@{
            certificateChainPath = $certificate
            privateKeyPath = $privateKey
        }
        trustedServerCertificate = [ordered]@{
            certificatePath = $trustedServerPath
        }
    }
    [IO.File]::WriteAllText(
        $template, ($sourceProfile | ConvertTo-Json -Depth 8), $utf8)
    $templateCreated = $true
    Set-PrivateFile $template

    Copy-Item -LiteralPath $enrollmentPath `
        -Destination (Join-Path $script:rollback "enrollment.original")
    Copy-Item -LiteralPath $authorizationPath `
        -Destination (Join-Path $script:rollback "authorization-policy.original")
    Copy-Item -LiteralPath $applicationPath `
        -Destination (Join-Path $script:rollback "application-profile.original")

    $entries = @(
        [ordered]@{ name = "stagingDirectory"; path = $staging; existed = $false },
        [ordered]@{ name = "certificate"; path = $certificate; existed = $false },
        [ordered]@{ name = "privateKey"; path = $privateKey; existed = $false },
        [ordered]@{ name = "pythonProfile"; path = $profile; existed = $false },
        [ordered]@{ name = "transferArchive"; path = $transfer; existed = $false },
        [ordered]@{ name = "enrollment"; path = $enrollmentPath; existed = $true;
            sha256 = (Get-FileHash -LiteralPath $enrollmentPath -Algorithm SHA256).Hash.ToLowerInvariant() },
        [ordered]@{ name = "authorizationPolicy"; path = $authorizationPath; existed = $true;
            sha256 = (Get-FileHash -LiteralPath $authorizationPath -Algorithm SHA256).Hash.ToLowerInvariant() },
        [ordered]@{ name = "applicationProfile"; path = $applicationPath; existed = $true;
            sha256 = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    )
    $preservedPrincipals = @(
        $enrollment.enrollments | ForEach-Object { [string]$_.principalId } |
            Sort-Object -Unique)
    $plan = [ordered]@{
        schemaVersion = 1
        purpose = "hase-minipc-laptop-python-credential-transaction"
        repositoryHead = $head
        signingRootThumbprint = [string]$authority.thumbprint
        trustPolicyId = [string]@($enrollment.enrollments)[0].trustPolicyId
        laptopPrincipal = $laptopPrincipal
        laptopGrants = $laptopPermissions
        validityDays = $ValidityDays
        profileTemplatePath = $template
        preservedPrincipalCount = $preservedPrincipals.Count
        entries = $entries
    }
    [IO.File]::WriteAllText(
        (Join-Path $script:rollback "transaction-plan.json"),
        ($plan | ConvertTo-Json -Depth 10), $utf8)
    foreach ($file in Get-ChildItem -LiteralPath $script:rollback -File)
    { Set-PrivateFile $file.FullName }

    Write-Host "Repository baseline ready       : True"
    Write-Host "Runtime processes stopped       : True"
    Write-Host "Dedicated authority ready       : True"
    Write-Host "Laptop principal remains absent : True"
    Write-Host "MiniPC local Python preserved   : True"
    Write-Host "Existing Client access preserved: True"
    Write-Host "Two Laptop grants prepared      : True"
    Write-Host "Laptop profile template prepared: True"
    Write-Host "Eight-entry transaction prepared: True"
    Write-Host "Rollback evidence secured       : True"
    Write-Host "Publication state unchanged     : True"
    Write-Host "Laptop transaction ready        : True"
}
catch
{
    if ($templateCreated -and (Test-Path -LiteralPath $template))
    { Remove-Item -LiteralPath $template -Force }
    if ($rollbackCreated -and (Test-Path -LiteralPath $script:rollback))
    { Remove-Item -LiteralPath $script:rollback -Recurse -Force }
    Write-Error "MiniPC Laptop Python credential transaction preparation failed."
    exit 1
}
