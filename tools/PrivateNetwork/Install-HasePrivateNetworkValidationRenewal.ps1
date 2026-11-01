[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ExpectedComputer,

    [Parameter(Mandatory)]
    [string] $BundleDirectory,

    [Parameter(Mandatory)]
    [string] $ClientConfigurationPath,

    [Parameter()]
    [string[]] $PinnedServerCertificatePath = @(),

    [Parameter(Mandatory)]
    [string] $CustodyDirectory,

    [Parameter()]
    [switch] $PreflightOnly,

    # An isolated certificate store for tests, used for the client and the
    # server certificate alike. The operator omits it.
    [Parameter()]
    [string] $CertificateStoreName = ""
)

# ADR-0086, the Laptop: after Install-HasePrivateNetworkValidationClient.ps1
# imported the renewed bundle, point the client configuration at the new
# certificates and replace the pinned server certificate files. Prints
# states and dates only.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HasePrivateNetworkRenewal.Common.ps1")

Assert-HaseRenewalComputer $ExpectedComputer

$now = [DateTimeOffset]::UtcNow
$problems = [System.Collections.Generic.List[string]]::new()

function Resolve-HaseRenewalStoreName {
    param($Reference)

    if (-not [string]::IsNullOrEmpty($CertificateStoreName)) {
        return $CertificateStoreName
    }
    return [string]$Reference.storeName
}

$running = @(Get-HaseRenewalRunningProcessNames)
Format-HaseRenewalLine "HASE processes stopped" ($running.Count -eq 0)
if ($running.Count -ne 0) {
    $problems.Add("processes-running")
}

$bundlePath = [System.IO.Path]::GetFullPath($BundleDirectory)
$bundleConfigurationPath = [System.IO.Path]::Combine($bundlePath, "laptop-private-network.json")
$bundleServerPath = [System.IO.Path]::Combine($bundlePath, "runtime-host-server.cer")
$bundle = $null
$bundleServer = $null
if ([System.IO.File]::Exists($bundleConfigurationPath) -and [System.IO.File]::Exists($bundleServerPath)) {
    $bundle = Read-HaseRenewalJson $bundleConfigurationPath
    if (-not (Test-HaseRenewalClientConfiguration $bundle)) {
        $bundle = $null
    }
    $bundleServer =
        [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            [System.IO.File]::ReadAllBytes($bundleServerPath))
}
if ($null -eq $bundle -or $null -eq $bundleServer -or
    $bundleServer.Thumbprint -ne $bundle.trustedServerCertificate.thumbprint) {
    Format-HaseRenewalLine "Bundle" "incomplete or inconsistent"
    $problems.Add("bundle-invalid")
    $bundle = $null
}
else {
    Format-HaseRenewalLine "Bundle server certificate" (Format-HaseRenewalValidity $bundleServer)
}

$configurationPath = [System.IO.Path]::GetFullPath($ClientConfigurationPath)
$configuration = $null
if ([System.IO.File]::Exists($configurationPath)) {
    $configuration = Read-HaseRenewalJson $configurationPath
    if (-not (Test-HaseRenewalClientConfiguration $configuration)) {
        $configuration = $null
    }
}
if ($null -eq $configuration) {
    Format-HaseRenewalLine "Client configuration" "not readable"
    $problems.Add("client-configuration-invalid")
}

$currentServer = $null
if ($null -ne $configuration) {
    $found = @(Find-HaseRenewalCertificate `
        (Resolve-HaseRenewalStoreName $configuration.trustedServerCertificate) `
        ([string]$configuration.trustedServerCertificate.storeLocation) `
        $configuration.trustedServerCertificate.thumbprint)
    if ($found.Count -eq 1) {
        $currentServer = $found[0]
        Format-HaseRenewalLine "Current pinned server" (Format-HaseRenewalValidity $currentServer)
    }
    else {
        Format-HaseRenewalLine "Current pinned server" "not found"
        $problems.Add("current-server-certificate-not-found")
    }
}

if ($null -ne $bundle -and $null -ne $configuration) {
    $sameAddress = [string]$bundle.address -ceq [string]$configuration.address
    Format-HaseRenewalLine "Bundle names the same host" $sameAddress
    if (-not $sameAddress) {
        $problems.Add("address-mismatch")
    }
    $renewed =
        $bundle.clientCertificate.thumbprint -eq $configuration.clientCertificate.thumbprint -or
        $bundle.trustedServerCertificate.thumbprint -eq $configuration.trustedServerCertificate.thumbprint
    if ($renewed) {
        $problems.Add("already-renewed")
    }

    $client = @(Find-HaseRenewalCertificate `
        (Resolve-HaseRenewalStoreName $bundle.clientCertificate) `
        ([string]$bundle.clientCertificate.storeLocation) `
        $bundle.clientCertificate.thumbprint)
    $clientReady =
        $client.Count -eq 1 -and $client[0].HasPrivateKey -and
        (Test-HaseRenewalCertificateCurrent $client[0] $now)
    Format-HaseRenewalLine "New client certificate imported" $clientReady
    if (-not $clientReady) {
        $problems.Add("client-certificate-not-imported")
    }
    $trusted = @(Find-HaseRenewalCertificate `
        (Resolve-HaseRenewalStoreName $bundle.trustedServerCertificate) `
        ([string]$bundle.trustedServerCertificate.storeLocation) `
        $bundle.trustedServerCertificate.thumbprint)
    Format-HaseRenewalLine "New server certificate imported" ($trusted.Count -eq 1)
    if ($trusted.Count -ne 1) {
        $problems.Add("server-certificate-not-imported")
    }
}

$pinPaths = @($PinnedServerCertificatePath | ForEach-Object { [System.IO.Path]::GetFullPath($_) })
$targetPaths = @($configurationPath) + $pinPaths
if (@($targetPaths | Sort-Object -Unique).Count -ne $targetPaths.Count) {
    $problems.Add("duplicate-target")
}
$pinsMatch = $true
foreach ($pinPath in $pinPaths) {
    if ($null -eq $currentServer -or -not [System.IO.File]::Exists($pinPath) -or
        (Get-HaseRenewalFileSha256 $pinPath) -ne (Get-HaseRenewalSha256 $currentServer.RawData)) {
        $pinsMatch = $false
    }
}
Format-HaseRenewalLine "Pinned files" $pinPaths.Count
Format-HaseRenewalLine "Pins match the current server" $pinsMatch
if (-not $pinsMatch) {
    $problems.Add("pin-foreign")
}

$custodyPath = [System.IO.Path]::GetFullPath($CustodyDirectory)
$custodyFree = -not (Test-Path -LiteralPath $custodyPath)
Format-HaseRenewalLine "Custody directory free" $custodyFree
if (-not $custodyFree) {
    $problems.Add("custody-directory-exists")
}

$ready = $problems.Count -eq 0
foreach ($problem in @($problems | Sort-Object -Unique)) {
    Format-HaseRenewalLine "Refused" $problem
}
Format-HaseRenewalLine "Ready for installation" $ready

if ($PreflightOnly) {
    if ($ready) { exit 0 } else { exit 1 }
}
if (-not $ready) {
    throw "installation-refused: the preflight did not pass."
}

$transactionId = [System.Guid]::NewGuid().ToString("N")
$stagedPath = [System.IO.Path]::Combine($custodyPath, "Staged")
$journalPath = [System.IO.Path]::Combine($custodyPath, $script:HaseRenewalJournalName)
[void][System.IO.Directory]::CreateDirectory($stagedPath)

$configuration.clientCertificate.thumbprint = $bundle.clientCertificate.thumbprint
$configuration.trustedServerCertificate.thumbprint = $bundle.trustedServerCertificate.thumbprint
$targets = @()
$index = 1
$targets += New-HaseRenewalTarget `
    -Path $configurationPath `
    -CandidateBytes (ConvertTo-HaseRenewalJsonBytes $configuration) `
    -StagedDirectory $stagedPath `
    -Index $index `
    -TransactionId $transactionId
foreach ($pinPath in $pinPaths) {
    $index++
    $targets += New-HaseRenewalTarget `
        -Path $pinPath `
        -CandidateBytes $bundleServer.RawData `
        -StagedDirectory $stagedPath `
        -Index $index `
        -TransactionId $transactionId
}

$journal = [pscustomobject][ordered]@{
    formatVersion = 1
    kind = "laptop"
    transactionId = $transactionId
    computer = $env:COMPUTERNAME
    createdUtc = $now.ToString("o")
    phase = "prepared"
    serverCertificateSha256 = (Get-HaseRenewalSha256 $bundleServer.RawData)
    addedCertificate = $null
    targets = @($targets)
}
Save-HaseRenewalJournal $journalPath $journal

Invoke-HaseRenewalPublication $journalPath

# Independent check of the result.
$installed = Read-HaseRenewalJson $configurationPath
$verified = (Test-HaseRenewalClientConfiguration $installed)
if ($verified) {
    $client = @(Find-HaseRenewalCertificate `
        (Resolve-HaseRenewalStoreName $installed.clientCertificate) `
        ([string]$installed.clientCertificate.storeLocation) `
        $installed.clientCertificate.thumbprint)
    $trusted = @(Find-HaseRenewalCertificate `
        (Resolve-HaseRenewalStoreName $installed.trustedServerCertificate) `
        ([string]$installed.trustedServerCertificate.storeLocation) `
        $installed.trustedServerCertificate.thumbprint)
    $verified =
        $client.Count -eq 1 -and $client[0].HasPrivateKey -and
        $trusted.Count -eq 1 -and
        (Get-HaseRenewalSha256 $trusted[0].RawData) -eq $journal.serverCertificateSha256
}
foreach ($pinPath in $pinPaths) {
    if ((Get-HaseRenewalFileSha256 $pinPath) -ne $journal.serverCertificateSha256) {
        $verified = $false
    }
}

Format-HaseRenewalLine "Installation published" $true
Format-HaseRenewalLine "Transaction" $transactionId
Format-HaseRenewalLine "Journal" $journalPath
Format-HaseRenewalLine "Installation verified" $verified
if (-not $verified) {
    throw "installation-unverified: the published configuration does not resolve to the renewed certificates."
}
exit 0
