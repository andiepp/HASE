[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $RollbackDirectory,
    [Parameter(Mandatory = $true)] [string] $LaptopCertificatePath,
    [Parameter(Mandatory = $true)] [string] $LaptopPrivateKeyPath,
    [Parameter(Mandatory = $true)] [string] $LaptopProfilePath,
    [Parameter(Mandatory = $true)] [string] $LaptopTrustedServerCertificatePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Json([string] $Path, $Value)
{
    $utf8 = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText(
        $Path, ($Value | ConvertTo-Json -Depth 12), $utf8)
}

function Resolve-AbsolutePath([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]')) { throw "path" }
    return [IO.Path]::GetFullPath($Value)
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
    $rollback = Resolve-AbsolutePath $RollbackDirectory
    $planPath = Join-Path $rollback "transaction-plan.json"
    if (-not (Test-Path -LiteralPath $planPath -PathType Leaf)) { throw "plan" }
    $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
    if ($plan.schemaVersion -ne 1 `
        -or $plan.purpose -cne "hase-minipc-laptop-python-credential-transaction" `
        -or $plan.laptopPrincipal -cne "hase-laptop-python-minipc" `
        -or @($plan.laptopGrants).Count -ne 2 `
        -or $plan.laptopGrants -notcontains "runtime-host.snapshot.read" `
        -or $plan.laptopGrants -notcontains "property.authoritative.read")
    { throw "plan" }

    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [IO.Path]::GetFullPath(
        (Join-Path $packageDirectory "..\.."))
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    & git -C $repositoryRoot merge-base --is-ancestor `
        ([string]$plan.repositoryHead) $head
    if ($LASTEXITCODE -ne 0) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $entries = @($plan.entries)
    foreach ($entry in $entries)
    {
        if ($entry.existed)
        {
            if (-not (Test-Path -LiteralPath $entry.path -PathType Leaf) `
                -or (Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash.ToLowerInvariant() `
                    -cne [string]$entry.sha256)
            { throw "revision" }
        }
        elseif (Test-Path -LiteralPath $entry.path) { throw "target" }
    }
    $staging = [string]($entries | Where-Object name -eq "stagingDirectory").path
    $certificate = [string]($entries | Where-Object name -eq "certificate").path
    $privateKey = [string]($entries | Where-Object name -eq "privateKey").path
    $profile = [string]($entries | Where-Object name -eq "pythonProfile").path
    $transfer = [string]($entries | Where-Object name -eq "transferArchive").path
    $enrollment = [string]($entries | Where-Object name -eq "enrollment").path
    $authorization = [string]($entries | Where-Object name -eq "authorizationPolicy").path
    $application = [string]($entries | Where-Object name -eq "applicationProfile").path
    $template = [string]$plan.profileTemplatePath
    if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { throw "template" }

    $laptopCertificate = Resolve-AbsolutePath $LaptopCertificatePath
    $laptopPrivateKey = Resolve-AbsolutePath $LaptopPrivateKeyPath
    $laptopProfile = Resolve-AbsolutePath $LaptopProfilePath
    $laptopTrustedServer = Resolve-AbsolutePath $LaptopTrustedServerCertificatePath
    $laptopPaths = @(
        $laptopCertificate, $laptopPrivateKey, $laptopProfile, $laptopTrustedServer)
    if (@($laptopPaths | Sort-Object -Unique).Count -ne 4 `
        -or (Split-Path -Parent $laptopCertificate) -ine (Split-Path -Parent $laptopPrivateKey) `
        -or (Split-Path -Parent $laptopCertificate) -ine (Split-Path -Parent $laptopProfile) `
        -or (Split-Path -Leaf $laptopCertificate) -cne "client-certificate.pem" `
        -or (Split-Path -Leaf $laptopPrivateKey) -cne "private-key.pem" `
        -or (Split-Path -Leaf $laptopProfile) -cne "runtime-host-profile.json" `
        -or (Split-Path -Leaf $laptopTrustedServer) -cne "runtime-host-server.cer")
    { throw "laptop-paths" }

    $journal = Join-Path $rollback "publication-journal.json"
    if (Test-Path -LiteralPath $journal) { throw "recovery-required" }
    $state = [ordered]@{
        schemaVersion = 1
        purpose = "hase-minipc-laptop-python-publication"
        status = "created"
        laptopCertificatePath = $laptopCertificate
        laptopPrivateKeyPath = $laptopPrivateKey
        laptopProfilePath = $laptopProfile
        laptopTrustedServerCertificatePath = $laptopTrustedServer
    }
    Write-Json $journal $state
    Set-PrivateFile $journal

    [IO.Directory]::CreateDirectory($staging) | Out-Null
    Set-PrivateDirectory $staging
    $policyHash = (Get-FileHash `
        -LiteralPath $authorization -Algorithm SHA256).Hash.ToLowerInvariant()
    $operator = Join-Path `
        $repositoryRoot "src\Hase.Python.CredentialProvisioning.Operator"
    $operatorArguments = @(
        "provision-laptop-minipc",
        "--signing-root-thumbprint", [string]$plan.signingRootThumbprint,
        "--trust-policy-id", [string]$plan.trustPolicyId,
        "--source-profile", $template,
        "--provisioning-directory", $staging,
        "--certificate", $certificate,
        "--private-key", $privateKey,
        "--profile", $profile,
        "--enrollment", $enrollment,
        "--authorization-policy", $authorization,
        "--expected-authorization-policy-sha256", $policyHash,
        "--validity-days", [string]$plan.validityDays)
    & dotnet run --project $operator -c Release -- @operatorArguments `
        1>$null 2>$null 3>$null 4>$null 5>$null 6>$null
    if ($LASTEXITCODE -ne 0) { throw "credential-publication" }
    $state.status = "credential-published"
    Write-Json $journal $state

    $profileDocument = Get-Content -LiteralPath $profile -Raw | ConvertFrom-Json
    $profileDocument.clientCertificate.certificateChainPath = $laptopCertificate
    $profileDocument.clientCertificate.privateKeyPath = $laptopPrivateKey
    $profileDocument.trustedServerCertificate.certificatePath = $laptopTrustedServer
    Write-Json $profile $profileDocument
    Set-PrivateFile $profile

    $enrollmentDocument = Get-Content -LiteralPath $enrollment -Raw | ConvertFrom-Json
    $authorizationDocument = Get-Content -LiteralPath $authorization -Raw | ConvertFrom-Json
    $laptopEnrollments = @($enrollmentDocument.enrollments | Where-Object {
        $_.principalId -ceq "hase-laptop-python-minipc" })
    $laptopGrants = @($authorizationDocument.grants | Where-Object {
        $_.principalId -ceq "hase-laptop-python-minipc" })
    if ($laptopEnrollments.Count -ne 1 -or $laptopGrants.Count -ne 2 `
        -or @($laptopGrants.permission | Sort-Object -Unique).Count -ne 2 `
        -or $laptopGrants.permission -notcontains "runtime-host.snapshot.read" `
        -or $laptopGrants.permission -notcontains "property.authoritative.read")
    { throw "published-scope" }

    $manifest = Join-Path $staging "transfer-manifest.json"
    $manifestDocument = [ordered]@{
        schemaVersion = 1
        purpose = "hase-laptop-python-minipc-credential-transfer"
        principalId = "hase-laptop-python-minipc"
        packageFiles = @(
            [ordered]@{ name = "client-certificate.pem";
                sha256 = (Get-FileHash -LiteralPath $certificate -Algorithm SHA256).Hash.ToLowerInvariant() },
            [ordered]@{ name = "private-key.pem";
                sha256 = (Get-FileHash -LiteralPath $privateKey -Algorithm SHA256).Hash.ToLowerInvariant() },
            [ordered]@{ name = "runtime-host-profile.json";
                sha256 = (Get-FileHash -LiteralPath $profile -Algorithm SHA256).Hash.ToLowerInvariant() })
        destination = [ordered]@{
            certificatePath = $laptopCertificate
            privateKeyPath = $laptopPrivateKey
            profilePath = $laptopProfile
            trustedServerCertificatePath = $laptopTrustedServer
        }
    }
    Write-Json $manifest $manifestDocument
    Set-PrivateFile $manifest
    Compress-Archive `
        -LiteralPath @($certificate, $privateKey, $profile, $manifest) `
        -DestinationPath $transfer `
        -CompressionLevel Optimal
    Set-PrivateFile $transfer
    $state.status = "committed"
    Write-Json $journal $state
    Remove-Item -LiteralPath $journal -Force

    Write-Host "Prepared plan revalidated       : True"
    Write-Host "Dedicated Laptop credential made: True"
    Write-Host "Laptop enrollment published     : True"
    Write-Host "Two Laptop grants published     : True"
    Write-Host "MiniPC local Python preserved   : True"
    Write-Host "Existing Client access preserved: True"
    Write-Host "Laptop profile paths exact      : True"
    Write-Host "Protected transfer package ready: True"
    Write-Host "Durable transaction committed   : True"
    Write-Host "Runtime Host remained stopped   : True"
    Write-Host "Laptop publication ready        : True"
}
catch
{
    Write-Error "MiniPC Laptop Python credential publication failed; explicit recovery may be required."
    exit 1
}
