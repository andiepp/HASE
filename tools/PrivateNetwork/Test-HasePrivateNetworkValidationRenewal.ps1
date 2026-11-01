[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $DesktopConfigurationPath,

    [Parameter()]
    [string[]] $PinnedServerCertificatePath = @(),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ClientPrincipalId = "laptop-validation-client",

    [Parameter()]
    [string] $JournalPath = "",

    # An isolated certificate store for tests. The operator omits it.
    [Parameter()]
    [string] $CertificateStoreName = ""
)

# ADR-0086, read-only: the Desktop side of a private-network deployment is
# usable. Prints states and dates only.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HasePrivateNetworkRenewal.Common.ps1")

$now = [DateTimeOffset]::UtcNow
$problems = [System.Collections.Generic.List[string]]::new()

$documents = @()
foreach ($path in $DesktopConfigurationPath) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    $document = $null
    if ([System.IO.File]::Exists($fullPath)) {
        $document = Read-HaseRenewalJson $fullPath
    }
    if ($null -eq $document -or -not (Test-HaseRenewalDesktopConfiguration $document)) {
        $problems.Add("desktop-configuration-invalid")
        continue
    }
    $documents += $document
}

$server = $null
$first = $null
if ($documents.Count -gt 0) {
    $first = $documents[0]
    foreach ($document in $documents) {
        if ($document.serverCertificate.thumbprint -ne $first.serverCertificate.thumbprint -or
            $document.binding.address -cne $first.binding.address -or
            $document.clientEnrollmentFilePath -cne $first.clientEnrollmentFilePath) {
            $problems.Add("desktop-configurations-disagree")
        }
    }
    $storeName = [string]$first.serverCertificate.storeName
    if (-not [string]::IsNullOrEmpty($CertificateStoreName)) {
        $storeName = $CertificateStoreName
    }
    $found = @(Find-HaseRenewalCertificate $storeName ([string]$first.serverCertificate.storeLocation) $first.serverCertificate.thumbprint)
    if ($found.Count -eq 1) {
        $server = $found[0]
    }
}

if ($null -eq $server) {
    Format-HaseRenewalLine "Server certificate" "not found"
    $problems.Add("server-certificate-not-found")
}
else {
    $current = Test-HaseRenewalCertificateCurrent $server $now
    Format-HaseRenewalLine "Server certificate" (Format-HaseRenewalValidity $server)
    Format-HaseRenewalLine "Server certificate current" $current
    Format-HaseRenewalLine "Server key" $server.HasPrivateKey
    $identifies = @(Get-HaseRenewalIpAddresses $server) -contains
        ([System.Net.IPAddress]::Parse([string]$first.binding.address)).ToString()
    Format-HaseRenewalLine "Identifies the binding" $identifies
    $usage = Test-HaseRenewalUsage $server $script:HaseRenewalServerUsageOid
    Format-HaseRenewalLine "Server authentication" $usage
    if (-not $current) { $problems.Add("server-certificate-not-current") }
    if (-not $server.HasPrivateKey) { $problems.Add("server-key-missing") }
    if (-not $identifies) { $problems.Add("server-identity-mismatch") }
    if (-not $usage) { $problems.Add("server-usage-missing") }
}

$enrollment = $null
if ($null -ne $first) {
    $enrollmentPath = [System.IO.Path]::GetFullPath([string]$first.clientEnrollmentFilePath)
    if ([System.IO.File]::Exists($enrollmentPath)) {
        $registry = Read-HaseRenewalJson $enrollmentPath
        if (Test-HaseRenewalEnrollmentRegistry $registry) {
            $principalEnrollments = @(@($registry.enrollments) | Where-Object { $_.principalId -ceq $ClientPrincipalId })
            if ($principalEnrollments.Count -eq 1) {
                $enrollment = $principalEnrollments[0]
            }
        }
    }
}
Format-HaseRenewalLine "Principal enrolled once" ($null -ne $enrollment)
if ($null -eq $enrollment) {
    $problems.Add("principal-not-enrolled-once")
}

$pinsMatch = $true
foreach ($pinPath in $PinnedServerCertificatePath) {
    $fullPath = [System.IO.Path]::GetFullPath($pinPath)
    if ($null -eq $server -or -not [System.IO.File]::Exists($fullPath) -or
        (Get-HaseRenewalFileSha256 $fullPath) -ne (Get-HaseRenewalSha256 $server.RawData)) {
        $pinsMatch = $false
    }
}
Format-HaseRenewalLine "Pins match the server" $pinsMatch
if (-not $pinsMatch) {
    $problems.Add("pin-mismatch")
}

if (-not [string]::IsNullOrEmpty($JournalPath)) {
    $journal = Read-HaseRenewalJson ([System.IO.Path]::GetFullPath($JournalPath))
    Format-HaseRenewalLine "Journal phase" $journal.phase
    if ($journal.phase -ne "published" -and $journal.phase -ne "cleaned") {
        $problems.Add("journal-not-published")
    }
    $targetsPublished = $true
    foreach ($target in @($journal.targets)) {
        if (-not [System.IO.File]::Exists($target.path) -or
            (Get-HaseRenewalFileSha256 $target.path) -ne $target.candidateSha256) {
            $targetsPublished = $false
        }
    }
    Format-HaseRenewalLine "Targets are the candidates" $targetsPublished
    if (-not $targetsPublished) {
        $problems.Add("target-not-candidate")
    }
    if ($journal.kind -eq "desktop") {
        if ($null -eq $server -or
            (Get-HaseRenewalSha256 $server.RawData) -ne $journal.serverCertificateSha256) {
            $problems.Add("server-certificate-not-renewed")
        }
        $renewedEnrollment =
            $null -ne $enrollment -and
            $enrollment.credentialId -ceq $journal.clientCredentialId
        Format-HaseRenewalLine "Renewed client enrolled" $renewedEnrollment
        if (-not $renewedEnrollment) {
            $problems.Add("client-not-renewed")
        }
    }
}

$verified = $problems.Count -eq 0
foreach ($problem in @($problems | Sort-Object -Unique)) {
    Format-HaseRenewalLine "Rejected" $problem
}
Format-HaseRenewalLine "Renewal verified" $verified
if ($verified) { exit 0 } else { exit 1 }
