[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ExpectedComputer,

    [Parameter(Mandatory)]
    [string[]] $DesktopConfigurationPath,

    [Parameter(Mandatory)]
    [string] $LaptopConfigurationPath,

    [Parameter()]
    [string[]] $PinnedServerCertificatePath = @(),

    [Parameter(Mandatory)]
    [string] $OutputDirectory,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ClientPrincipalId = "laptop-validation-client",

    [Parameter()]
    [ValidateRange(1, 397)]
    [int] $ValidityDays = 90,

    [Parameter()]
    [System.Security.SecureString] $TransferPassword,

    [Parameter()]
    [switch] $PreflightOnly,

    # An isolated certificate store for tests. The operator omits it, and the
    # store named by the configuration is used.
    [Parameter()]
    [string] $CertificateStoreName = "",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $TrustedRootStoreName = "Root"
)

# ADR-0086: renews the server certificate and the Laptop client certificate
# of a private-network deployment under its existing root. Prints states and
# dates only: no thumbprint, address, credential ID or key material.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HasePrivateNetworkRenewal.Common.ps1")

Assert-HaseRenewalComputer $ExpectedComputer

$now = [DateTimeOffset]::UtcNow
$problems = [System.Collections.Generic.List[string]]::new()

$running = @(Get-HaseRenewalRunningProcessNames)
Format-HaseRenewalLine "HASE processes stopped" ($running.Count -eq 0)
if ($running.Count -ne 0) {
    $problems.Add("processes-running")
}

# The Desktop configurations: all must name the same certificate, binding
# and enrollment registry.
$configurations = @()
foreach ($path in $DesktopConfigurationPath) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not [System.IO.File]::Exists($fullPath)) {
        $problems.Add("desktop-configuration-missing")
        continue
    }
    $document = Read-HaseRenewalJson $fullPath
    if (-not (Test-HaseRenewalDesktopConfiguration $document)) {
        $problems.Add("desktop-configuration-invalid")
        continue
    }
    $configurations += [pscustomobject]@{ Path = $fullPath; Document = $document }
}
Format-HaseRenewalLine "Desktop configurations" $configurations.Count

$first = $null
if ($configurations.Count -gt 0) {
    $first = $configurations[0].Document
    foreach ($configuration in $configurations) {
        $document = $configuration.Document
        $agrees =
            $document.serverCertificate.thumbprint -eq $first.serverCertificate.thumbprint -and
            $document.serverCertificate.storeName -ceq $first.serverCertificate.storeName -and
            $document.serverCertificate.storeLocation -ceq $first.serverCertificate.storeLocation -and
            $document.binding.address -ceq $first.binding.address -and
            [int]$document.binding.port -eq [int]$first.binding.port -and
            $document.clientEnrollmentFilePath -ceq $first.clientEnrollmentFilePath
        if (-not $agrees) {
            $problems.Add("desktop-configurations-disagree")
        }
    }
}

$pinPaths = @($PinnedServerCertificatePath | ForEach-Object { [System.IO.Path]::GetFullPath($_) })
$targetPaths = @($configurations | ForEach-Object { $_.Path }) + $pinPaths
if (@($targetPaths | Sort-Object -Unique).Count -ne $targetPaths.Count) {
    $problems.Add("duplicate-target")
}

$oldServer = $null
$root = $null
$storeName = ""
$storeLocation = ""
$bindingAddress = $null
if ($null -ne $first) {
    $storeLocation = [string]$first.serverCertificate.storeLocation
    $storeName = [string]$first.serverCertificate.storeName
    if (-not [string]::IsNullOrEmpty($CertificateStoreName)) {
        $storeName = $CertificateStoreName
    }
    if ($storeLocation -cne "CurrentUser") {
        $problems.Add("store-location-unsupported")
    }

    $parsedAddress = $null
    if ([System.Net.IPAddress]::TryParse([string]$first.binding.address, [ref]$parsedAddress)) {
        $bindingAddress = $parsedAddress
    }
    else {
        $problems.Add("binding-address-invalid")
    }

    if ($storeLocation -ceq "CurrentUser") {
        $found = @(Find-HaseRenewalCertificate $storeName $storeLocation $first.serverCertificate.thumbprint)
        if ($found.Count -eq 1) {
            $oldServer = $found[0]
        }
    }
}

if ($null -eq $oldServer) {
    Format-HaseRenewalLine "Server certificate" "not found"
    $problems.Add("server-certificate-not-found")
}
else {
    $serverState = "Valid"
    if (-not (Test-HaseRenewalCertificateCurrent $oldServer $now)) {
        $serverState = "Expired"
        if ($oldServer.NotBefore.ToUniversalTime() -gt $now.UtcDateTime) {
            $serverState = "NotYetValid"
        }
    }
    Format-HaseRenewalLine "Server certificate" ("{0}, {1}" -f $serverState, (Format-HaseRenewalValidity $oldServer))

    $roots = @(Find-HaseRenewalIssuingRoot $oldServer $storeName $storeLocation)
    if ($roots.Count -eq 1) {
        $root = $roots[0]
    }
}

if ($null -eq $root) {
    Format-HaseRenewalLine "Issuing root with key" "not found"
    $problems.Add("root-not-found")
}
else {
    Format-HaseRenewalLine "Issuing root with key" (Format-HaseRenewalValidity $root)
    $trusted = @(Find-HaseRenewalCertificate $TrustedRootStoreName "CurrentUser" $root.Thumbprint)
    Format-HaseRenewalLine "Root trusted" ($trusted.Count -eq 1)
    if ($trusted.Count -ne 1) {
        $problems.Add("root-not-trusted")
    }
    $covers = $root.NotAfter.ToUniversalTime() -ge $now.AddDays($ValidityDays).UtcDateTime
    Format-HaseRenewalLine "Root covers $ValidityDays days" $covers
    if (-not $covers) {
        $problems.Add("root-does-not-cover-validity")
    }
    $rootKey = $null
    try {
        $rootKey =
            [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey(
                $root)
    }
    catch {
        $rootKey = $null
    }
    Format-HaseRenewalLine "Root key accessible" ($null -ne $rootKey)
    if ($null -eq $rootKey) {
        $problems.Add("root-key-unavailable")
    }
}

# The enrollment registry: the principal exactly once.
$enrollmentPath = $null
$enrollmentDocument = $null
if ($null -ne $first) {
    $enrollmentPath = [System.IO.Path]::GetFullPath([string]$first.clientEnrollmentFilePath)
    if ([System.IO.File]::Exists($enrollmentPath)) {
        $enrollmentDocument = Read-HaseRenewalJson $enrollmentPath
        if (-not (Test-HaseRenewalEnrollmentRegistry $enrollmentDocument)) {
            $enrollmentDocument = $null
        }
    }
}
if ($null -eq $enrollmentDocument) {
    Format-HaseRenewalLine "Enrollment registry" "not readable"
    $problems.Add("enrollment-registry-invalid")
}
else {
    $principalEnrollments = @(@($enrollmentDocument.enrollments) | Where-Object { $_.principalId -ceq $ClientPrincipalId })
    Format-HaseRenewalLine "Enrollments" @($enrollmentDocument.enrollments).Count
    Format-HaseRenewalLine "Principal enrolled once" ($principalEnrollments.Count -eq 1)
    if ($principalEnrollments.Count -ne 1) {
        $problems.Add("principal-not-enrolled-once")
    }
    if ($targetPaths -contains $enrollmentPath) {
        $problems.Add("duplicate-target")
    }
}

# The Laptop configuration, used as the template of the Laptop's new one.
$laptopDocument = $null
$laptopPath = [System.IO.Path]::GetFullPath($LaptopConfigurationPath)
if ([System.IO.File]::Exists($laptopPath)) {
    $laptopDocument = Read-HaseRenewalJson $laptopPath
    if (-not (Test-HaseRenewalClientConfiguration $laptopDocument)) {
        $laptopDocument = $null
    }
}
if ($null -eq $laptopDocument) {
    Format-HaseRenewalLine "Laptop configuration" "not readable"
    $problems.Add("laptop-configuration-invalid")
}
elseif ($null -ne $first -and $null -ne $bindingAddress) {
    $sameAddress = $false
    $uri = $null
    if ([System.Uri]::TryCreate([string]$laptopDocument.address, [System.UriKind]::Absolute, [ref]$uri)) {
        $laptopAddress = $null
        if ([System.Net.IPAddress]::TryParse($uri.Host.Trim("[", "]"), [ref]$laptopAddress)) {
            $sameAddress =
                $laptopAddress.Equals($bindingAddress) -and
                $uri.Port -eq [int]$first.binding.port -and
                $uri.Scheme -ceq "https"
        }
    }
    Format-HaseRenewalLine "Laptop names the binding" $sameAddress
    if (-not $sameAddress) {
        $problems.Add("laptop-address-mismatch")
    }
    $samePin = $laptopDocument.trustedServerCertificate.thumbprint -eq $first.serverCertificate.thumbprint
    Format-HaseRenewalLine "Laptop pins the server" $samePin
    if (-not $samePin) {
        $problems.Add("laptop-pin-mismatch")
    }
}

# Every pinned file must be the current server certificate, byte for byte.
$pinsMatch = $true
foreach ($pinPath in $pinPaths) {
    if (-not [System.IO.File]::Exists($pinPath)) {
        $pinsMatch = $false
        continue
    }
    if ($null -ne $oldServer -and
        (Get-HaseRenewalFileSha256 $pinPath) -ne (Get-HaseRenewalSha256 $oldServer.RawData)) {
        $pinsMatch = $false
    }
}
Format-HaseRenewalLine "Pinned files" $pinPaths.Count
Format-HaseRenewalLine "Pins match the server" $pinsMatch
if (-not $pinsMatch) {
    $problems.Add("pin-foreign")
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputFree = -not (Test-Path -LiteralPath $outputPath)
Format-HaseRenewalLine "Output directory free" $outputFree
if (-not $outputFree) {
    $problems.Add("output-directory-exists")
}

$ready = $problems.Count -eq 0
foreach ($problem in @($problems | Sort-Object -Unique)) {
    Format-HaseRenewalLine "Refused" $problem
}
Format-HaseRenewalLine "Ready for renewal" $ready

if ($PreflightOnly) {
    if ($ready) { exit 0 } else { exit 1 }
}
if (-not $ready) {
    throw "renewal-refused: the preflight did not pass."
}

# The renewal.
$transactionId = [System.Guid]::NewGuid().ToString("N")
$stagedPath = [System.IO.Path]::Combine($outputPath, "Staged")
$laptopSetPath = [System.IO.Path]::Combine($outputPath, "Laptop")
$journalPath = [System.IO.Path]::Combine($outputPath, $script:HaseRenewalJournalName)
[void][System.IO.Directory]::CreateDirectory($stagedPath)
[void][System.IO.Directory]::CreateDirectory($laptopSetPath)

if ($null -eq $TransferPassword) {
    $TransferPassword =
        Read-Host `
            "Enter a transfer password for the laptop client credential" `
            -AsSecureString
}

$notBefore = $now.AddMinutes(-5)
$notAfter = $now.AddDays($ValidityDays)
$server = New-HaseRenewalCertificate `
    -Root $root `
    -Subject "CN=HASE Private Network Validation Server" `
    -UsageOid $script:HaseRenewalServerUsageOid `
    -KeyUsage ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment) `
    -IpAddress $bindingAddress `
    -NotBefore $notBefore `
    -NotAfter $notAfter
$client = New-HaseRenewalCertificate `
    -Root $root `
    -Subject "CN=HASE Private Network Validation Client" `
    -UsageOid $script:HaseRenewalClientUsageOid `
    -KeyUsage ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature) `
    -IpAddress $null `
    -NotBefore $notBefore `
    -NotAfter $notAfter
$clientCredentialId = "x509-sha256:" + (Get-HaseRenewalSha256 $client.RawData)

# The Laptop set.
[System.IO.File]::WriteAllBytes(
    [System.IO.Path]::Combine($laptopSetPath, "laptop-client.pfx"),
    $client.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
        $TransferPassword))
[System.IO.File]::WriteAllBytes(
    [System.IO.Path]::Combine($laptopSetPath, "runtime-host-server.cer"),
    $server.RawData)
$laptopDocument.clientCertificate.thumbprint = $client.Thumbprint
$laptopDocument.trustedServerCertificate.thumbprint = $server.Thumbprint
[System.IO.File]::WriteAllBytes(
    [System.IO.Path]::Combine($laptopSetPath, "laptop-private-network.json"),
    (ConvertTo-HaseRenewalJsonBytes $laptopDocument))

# The staged candidates: configurations, the enrollment registry, the pins.
$targets = @()
$index = 0
foreach ($configuration in $configurations) {
    $index++
    $configuration.Document.serverCertificate.thumbprint = $server.Thumbprint
    $targets += New-HaseRenewalTarget `
        -Path $configuration.Path `
        -CandidateBytes (ConvertTo-HaseRenewalJsonBytes $configuration.Document) `
        -StagedDirectory $stagedPath `
        -Index $index `
        -TransactionId $transactionId
}
foreach ($enrollment in @($enrollmentDocument.enrollments)) {
    if ($enrollment.principalId -ceq $ClientPrincipalId) {
        $enrollment.credentialId = $clientCredentialId
    }
}
$index++
$targets += New-HaseRenewalTarget `
    -Path $enrollmentPath `
    -CandidateBytes (ConvertTo-HaseRenewalJsonBytes $enrollmentDocument) `
    -StagedDirectory $stagedPath `
    -Index $index `
    -TransactionId $transactionId
foreach ($pinPath in $pinPaths) {
    $index++
    $targets += New-HaseRenewalTarget `
        -Path $pinPath `
        -CandidateBytes $server.RawData `
        -StagedDirectory $stagedPath `
        -Index $index `
        -TransactionId $transactionId
}

$journal = [pscustomobject][ordered]@{
    formatVersion = 1
    kind = "desktop"
    transactionId = $transactionId
    computer = $env:COMPUTERNAME
    createdUtc = $now.ToString("o")
    phase = "prepared"
    clientPrincipalId = $ClientPrincipalId
    clientCredentialId = $clientCredentialId
    serverCertificateSha256 = (Get-HaseRenewalSha256 $server.RawData)
    addedCertificate = $null
    targets = @($targets)
}
Save-HaseRenewalJournal $journalPath $journal

try {
    Add-HaseRenewalCertificateWithKey $server $storeName $storeLocation
    $journal.addedCertificate = [pscustomobject][ordered]@{
        storeName = $storeName
        storeLocation = $storeLocation
        thumbprint = $server.Thumbprint
    }
    Save-HaseRenewalJournal $journalPath $journal
}
catch {
    $failure = $_
    Remove-HaseRenewalCertificate $storeName $storeLocation $server.Thumbprint
    $journal.phase = "rolled-back"
    Save-HaseRenewalJournal $journalPath $journal
    throw $failure
}

Invoke-HaseRenewalPublication $journalPath

Format-HaseRenewalLine "Renewal published" $true
Format-HaseRenewalLine "Transaction" $transactionId
Format-HaseRenewalLine "New server certificate" (Format-HaseRenewalValidity $server)
Format-HaseRenewalLine "New client certificate" (Format-HaseRenewalValidity $client)
Format-HaseRenewalLine "Laptop set" $laptopSetPath
Format-HaseRenewalLine "Journal" $journalPath

$verifierArguments = @{
    DesktopConfigurationPath = @($configurations | ForEach-Object { $_.Path })
    PinnedServerCertificatePath = $pinPaths
    ClientPrincipalId = $ClientPrincipalId
    JournalPath = $journalPath
    CertificateStoreName = $CertificateStoreName
}
& (Join-Path $PSScriptRoot "Test-HasePrivateNetworkValidationRenewal.ps1") @verifierArguments
if ($LASTEXITCODE -ne 0) {
    throw "renewal-unverified: the verifier did not accept the renewal."
}
exit 0
