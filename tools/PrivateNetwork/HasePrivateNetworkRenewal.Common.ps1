# Shared by the ADR-0086 private-network renewal tools. Dot-sourced by them;
# it defines functions only and changes nothing when loaded.

$script:HaseRenewalJournalName =
    "private-network-renewal.transaction.json"
$script:HaseRenewalProcessPatterns =
    @("Hase.DesktopHost.App*", "Hase.Client.Wpf.App*")
$script:HaseRenewalServerUsageOid = "1.3.6.1.5.5.7.3.1"
$script:HaseRenewalClientUsageOid = "1.3.6.1.5.5.7.3.2"

function Assert-HaseRenewalComputer {
    param([string] $ExpectedComputer)

    if (-not ($env:COMPUTERNAME -ceq $ExpectedComputer)) {
        throw "computer-mismatch: this tool runs only on the computer named by -ExpectedComputer."
    }
}

function Get-HaseRenewalRunningProcessNames {
    $names = @()
    foreach ($pattern in $script:HaseRenewalProcessPatterns) {
        $names += @(
            Get-Process -Name $pattern -ErrorAction SilentlyContinue |
                ForEach-Object { $_.ProcessName })
    }
    return @($names | Sort-Object -Unique)
}

function Format-HaseRenewalLine {
    param([string] $Label, $Value)

    return ("{0,-32}: {1}" -f $Label, $Value)
}

function Format-HaseRenewalValidity {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    return ("{0:yyyy-MM-dd}..{1:yyyy-MM-dd}" -f
        $Certificate.NotBefore.ToUniversalTime(),
        $Certificate.NotAfter.ToUniversalTime())
}

function Test-HaseRenewalCertificateCurrent {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [DateTimeOffset] $AtUtc)

    $instant = $AtUtc.UtcDateTime
    return ($Certificate.NotBefore.ToUniversalTime() -le $instant -and
        $Certificate.NotAfter.ToUniversalTime() -ge $instant)
}

function Get-HaseRenewalSha256 {
    param([byte[]] $Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($Bytes)
    }
    finally {
        $sha.Dispose()
    }
    return (-join ($hash | ForEach-Object { $_.ToString("x2") }))
}

function Get-HaseRenewalFileSha256 {
    param([string] $Path)

    return (Get-HaseRenewalSha256 ([System.IO.File]::ReadAllBytes($Path)))
}

function Read-HaseRenewalJson {
    param([string] $Path)

    $text = [System.IO.File]::ReadAllText(
        $Path,
        [System.Text.Encoding]::UTF8)
    return ($text | ConvertFrom-Json)
}

function ConvertTo-HaseRenewalJsonBytes {
    param($Document)

    $text = ConvertTo-Json -InputObject $Document -Depth 8
    $encoding = [System.Text.UTF8Encoding]::new($false)
    return ,$encoding.GetBytes($text)
}

function Save-HaseRenewalJournal {
    param([string] $Path, $Journal)

    $bytes = ConvertTo-HaseRenewalJsonBytes $Journal
    $temporary = "$Path.tmp"
    [System.IO.File]::WriteAllBytes($temporary, $bytes)
    if ([System.IO.File]::Exists($Path)) {
        [System.IO.File]::Delete($Path)
    }
    [System.IO.File]::Move($temporary, $Path)
}

function Test-HaseRenewalProperty {
    param($Object, [string] $Name)

    if ($null -eq $Object) {
        return $false
    }
    if (-not ($Object -is [System.Management.Automation.PSCustomObject])) {
        return $false
    }
    return (@($Object.PSObject.Properties.Name) -contains $Name)
}

function Test-HaseRenewalStoreReference {
    param($Reference)

    foreach ($name in @("storeName", "storeLocation", "thumbprint")) {
        if (-not (Test-HaseRenewalProperty $Reference $name)) {
            return $false
        }
        if ([string]::IsNullOrWhiteSpace([string]$Reference.$name)) {
            return $false
        }
    }
    return $true
}

function Test-HaseRenewalDesktopConfiguration {
    param($Document)

    if (-not (Test-HaseRenewalProperty $Document "binding")) { return $false }
    if (-not (Test-HaseRenewalProperty $Document.binding "address")) { return $false }
    if (-not (Test-HaseRenewalProperty $Document.binding "port")) { return $false }
    if (-not (Test-HaseRenewalProperty $Document "serverCertificate")) { return $false }
    if (-not (Test-HaseRenewalStoreReference $Document.serverCertificate)) { return $false }
    if (-not (Test-HaseRenewalProperty $Document "clientEnrollmentFilePath")) { return $false }
    return $true
}

function Test-HaseRenewalClientConfiguration {
    param($Document)

    if (-not (Test-HaseRenewalProperty $Document "address")) { return $false }
    if (-not (Test-HaseRenewalProperty $Document "clientCertificate")) { return $false }
    if (-not (Test-HaseRenewalStoreReference $Document.clientCertificate)) { return $false }
    if (-not (Test-HaseRenewalProperty $Document "trustedServerCertificate")) { return $false }
    if (-not (Test-HaseRenewalStoreReference $Document.trustedServerCertificate)) { return $false }
    return $true
}

function Test-HaseRenewalEnrollmentRegistry {
    param($Document)

    if (-not (Test-HaseRenewalProperty $Document "formatVersion")) { return $false }
    if ([int]$Document.formatVersion -ne 1) { return $false }
    if (-not (Test-HaseRenewalProperty $Document "enrollments")) { return $false }
    foreach ($enrollment in @($Document.enrollments)) {
        foreach ($name in @("credentialId", "principalId", "trustPolicyId")) {
            if (-not (Test-HaseRenewalProperty $enrollment $name)) {
                return $false
            }
        }
    }
    return $true
}

function Open-HaseRenewalStore {
    param([string] $StoreName, [string] $StoreLocation, [switch] $Write)

    $location =
        [System.Security.Cryptography.X509Certificates.StoreLocation]$StoreLocation
    $store =
        [System.Security.Cryptography.X509Certificates.X509Store]::new(
            $StoreName,
            $location)
    if ($Write) {
        $store.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    }
    else {
        $store.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
    }
    return $store
}

function Find-HaseRenewalCertificate {
    param([string] $StoreName, [string] $StoreLocation, [string] $Thumbprint)

    $store = Open-HaseRenewalStore $StoreName $StoreLocation
    try {
        $found = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $Thumbprint,
            $false)
        return @($found)
    }
    finally {
        $store.Close()
    }
}

function Find-HaseRenewalIssuingRoot {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [string] $StoreName,
        [string] $StoreLocation)

    $store = Open-HaseRenewalStore $StoreName $StoreLocation
    try {
        $candidates = @(
            $store.Certificates |
                Where-Object {
                    $_.Subject -ceq $Certificate.Issuer -and $_.HasPrivateKey
                })
    }
    finally {
        $store.Close()
    }

    $roots = @()
    foreach ($candidate in $candidates) {
        $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
        try {
            $chain.ChainPolicy.RevocationMode =
                [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
            $chain.ChainPolicy.VerificationFlags =
                [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::AllowUnknownCertificateAuthority -bor
                [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::IgnoreNotTimeValid
            [void]$chain.ChainPolicy.ExtraStore.Add($candidate)
            [void]$chain.Build($Certificate)
            if ($chain.ChainElements.Count -ge 2 -and
                $chain.ChainElements[1].Certificate.Thumbprint -eq $candidate.Thumbprint) {
                $roots += $candidate
            }
        }
        finally {
            $chain.Dispose()
        }
    }
    return $roots
}

function Get-HaseRenewalIpAddresses {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate)

    # The IP entries ([7] OCTET STRING) of the Subject Alternative Name,
    # read from DER rather than from localized formatting.
    $addresses = @()
    foreach ($extension in $Certificate.Extensions) {
        if ($extension.Oid.Value -ne "2.5.29.17") {
            continue
        }
        $raw = $extension.RawData
        $offset = 0
        $read = {
            param([byte[]] $Data, [int] $Start)
            $length = [int]$Data[$Start + 1]
            $header = 2
            if ($length -band 0x80) {
                $count = $length -band 0x7F
                $length = 0
                for ($i = 0; $i -lt $count; $i++) {
                    $length = ($length * 256) + [int]$Data[$Start + 2 + $i]
                }
                $header = 2 + $count
            }
            return @($header, $length)
        }
        $outer = & $read $raw $offset
        $position = $offset + $outer[0]
        $end = $position + $outer[1]
        while ($position -lt $end) {
            $tag = $raw[$position]
            $item = & $read $raw $position
            if ($tag -eq 0x87) {
                $bytes = [byte[]]::new($item[1])
                [System.Array]::Copy($raw, $position + $item[0], $bytes, 0, $item[1])
                $addresses += ([System.Net.IPAddress]::new($bytes)).ToString()
            }
            $position = $position + $item[0] + $item[1]
        }
    }
    return $addresses
}

function Test-HaseRenewalUsage {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [string] $UsageOid)

    foreach ($extension in $Certificate.Extensions) {
        if ($extension.Oid.Value -ne "2.5.29.37") {
            continue
        }
        $usage =
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
                $extension,
                $extension.Critical)
        foreach ($oid in $usage.EnhancedKeyUsages) {
            if ($oid.Value -eq $UsageOid) {
                return $true
            }
        }
        return $false
    }
    return $false
}

function New-HaseRenewalCertificate {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Root,
        [string] $Subject,
        [string] $UsageOid,
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags] $KeyUsage,
        [System.Net.IPAddress] $IpAddress,
        [DateTimeOffset] $NotBefore,
        [DateTimeOffset] $NotAfter)

    $key = [System.Security.Cryptography.RSACng]::new(2048)
    $request =
        [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $Subject,
            $key,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new(
            $false, $false, 0, $true))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            $KeyUsage, $true))
    $usages = [System.Security.Cryptography.OidCollection]::new()
    [void]$usages.Add([System.Security.Cryptography.Oid]::new($UsageOid))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
            $usages, $true))
    $request.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new(
            $request.PublicKey, $false))

    foreach ($extension in $Root.Extensions) {
        if ($extension.Oid.Value -ne "2.5.29.14") {
            continue
        }
        $rootKeyIdentifier =
            [System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new(
                $extension, $false)
        $identifier = $rootKeyIdentifier.SubjectKeyIdentifier
        $identifierBytes = [byte[]]::new($identifier.Length / 2)
        for ($i = 0; $i -lt $identifierBytes.Length; $i++) {
            $identifierBytes[$i] =
                [System.Convert]::ToByte($identifier.Substring($i * 2, 2), 16)
        }
        $authority = [byte[]]::new($identifierBytes.Length + 4)
        $authority[0] = 0x30
        $authority[1] = [byte]($identifierBytes.Length + 2)
        $authority[2] = 0x80
        $authority[3] = [byte]$identifierBytes.Length
        [System.Array]::Copy($identifierBytes, 0, $authority, 4, $identifierBytes.Length)
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509Extension]::new(
                "2.5.29.35", $authority, $false))
    }

    if ($null -ne $IpAddress) {
        $alternativeNames =
            [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        $alternativeNames.AddIpAddress($IpAddress)
        $request.CertificateExtensions.Add($alternativeNames.Build($false))
    }

    $serial = [byte[]]::new(16)
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($serial)
    }
    finally {
        $random.Dispose()
    }
    $serial[0] = $serial[0] -band 0x7F

    $certificate = $request.Create($Root, $NotBefore, $NotAfter, $serial)
    return [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey(
        $certificate,
        $key)
}

function Add-HaseRenewalCertificateWithKey {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [string] $StoreName,
        [string] $StoreLocation)

    # The key is persisted by a PKCS#12 round trip and is not exportable.
    $password = [System.Guid]::NewGuid().ToString("N")
    $bytes = $Certificate.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
        $password)
    $flags =
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet -bor
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::UserKeySet
    $persisted =
        [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $bytes, $password, $flags)
    $store = Open-HaseRenewalStore $StoreName $StoreLocation -Write
    try {
        $store.Add($persisted)
    }
    finally {
        $store.Close()
    }
}

function Remove-HaseRenewalCertificate {
    param([string] $StoreName, [string] $StoreLocation, [string] $Thumbprint)

    $store = Open-HaseRenewalStore $StoreName $StoreLocation -Write
    try {
        $found = @($store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $Thumbprint,
            $false))
        foreach ($certificate in $found) {
            $key = $null
            if ($certificate.HasPrivateKey) {
                $key =
                    [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey(
                        $certificate)
            }
            $store.Remove($certificate)
            if ($key -is [System.Security.Cryptography.RSACng]) {
                $key.Key.Delete()
            }
            elseif ($key -is [System.Security.Cryptography.RSACryptoServiceProvider]) {
                $key.PersistKeyInCsp = $false
                $key.Clear()
            }
        }
    }
    finally {
        $store.Close()
    }
}

function New-HaseRenewalTarget {
    param(
        [string] $Path,
        [byte[]] $CandidateBytes,
        [string] $StagedDirectory,
        [int] $Index,
        [string] $TransactionId)

    $candidatePath = [System.IO.Path]::Combine(
        $StagedDirectory,
        ("{0:D2}-{1}" -f $Index, [System.IO.Path]::GetFileName($Path)))
    [System.IO.File]::WriteAllBytes($candidatePath, $CandidateBytes)
    return [pscustomobject][ordered]@{
        path = $Path
        originalSha256 = (Get-HaseRenewalFileSha256 $Path)
        candidatePath = $candidatePath
        candidateSha256 = (Get-HaseRenewalSha256 $CandidateBytes)
        backupPath = "$Path.renewal-$TransactionId.backup"
    }
}

function Undo-HaseRenewalTargets {
    param($Journal)

    foreach ($target in @($Journal.targets)) {
        $temporary = "$($target.path).renewal-$($Journal.transactionId).tmp"
        if ([System.IO.File]::Exists($temporary)) {
            [System.IO.File]::Delete($temporary)
        }
        if ([System.IO.File]::Exists($target.path) -and
            (Get-HaseRenewalFileSha256 $target.path) -eq $target.originalSha256) {
            continue
        }
        if (-not [System.IO.File]::Exists($target.backupPath)) {
            throw "backup-missing: a target differs from its original and has no backup."
        }
        if ((Get-HaseRenewalFileSha256 $target.backupPath) -ne $target.originalSha256) {
            throw "backup-changed: a backup no longer matches its original."
        }
        [System.IO.File]::Copy($target.backupPath, $target.path, $true)
        if ((Get-HaseRenewalFileSha256 $target.path) -ne $target.originalSha256) {
            throw "restore-unverified: a restored target does not match its original."
        }
    }
}

function Remove-HaseRenewalAddedCertificate {
    param($Journal)

    if ((Test-HaseRenewalProperty $Journal "addedCertificate") -and
        $null -ne $Journal.addedCertificate) {
        Remove-HaseRenewalCertificate `
            $Journal.addedCertificate.storeName `
            $Journal.addedCertificate.storeLocation `
            $Journal.addedCertificate.thumbprint
    }
}

function Invoke-HaseRenewalPublication {
    param([string] $JournalPath)

    $journal = Read-HaseRenewalJson $JournalPath
    if ($journal.phase -ne "prepared") {
        throw "journal-phase-unexpected: publication needs a prepared journal."
    }
    $journal.phase = "publishing"
    Save-HaseRenewalJournal $JournalPath $journal

    try {
        foreach ($target in @($journal.targets)) {
            if ((Get-HaseRenewalFileSha256 $target.path) -ne $target.originalSha256) {
                throw "target-changed: a target changed after the preflight."
            }
            if ((Get-HaseRenewalFileSha256 $target.candidatePath) -ne $target.candidateSha256) {
                throw "candidate-changed: a staged candidate changed."
            }
            [System.IO.File]::Copy($target.path, $target.backupPath, $false)
            if ((Get-HaseRenewalFileSha256 $target.backupPath) -ne $target.originalSha256) {
                throw "backup-unverified: a backup does not match its original."
            }
        }

        foreach ($target in @($journal.targets)) {
            $temporary = "$($target.path).renewal-$($journal.transactionId).tmp"
            [System.IO.File]::Copy($target.candidatePath, $temporary, $false)
            if ((Get-HaseRenewalFileSha256 $temporary) -ne $target.candidateSha256) {
                throw "candidate-copy-unverified"
            }
            [System.IO.File]::Copy($temporary, $target.path, $true)
            [System.IO.File]::Delete($temporary)
            if ((Get-HaseRenewalFileSha256 $target.path) -ne $target.candidateSha256) {
                throw "publication-unverified: a target does not match its candidate."
            }
        }

        $journal.phase = "published"
        Save-HaseRenewalJournal $JournalPath $journal
    }
    catch {
        $failure = $_
        Undo-HaseRenewalTargets $journal
        Remove-HaseRenewalAddedCertificate $journal
        $journal.phase = "rolled-back"
        Save-HaseRenewalJournal $JournalPath $journal
        throw $failure
    }
}
