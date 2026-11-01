[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ExpectedComputer,

    [Parameter(Mandatory)]
    [string] $JournalPath,

    [Parameter(Mandatory)]
    [ValidateSet("Rollback", "Cleanup")]
    [string] $Mode
)

# ADR-0086 recovery, from the journal of a Desktop renewal or a Laptop
# installation. Rollback restores every target to its original and removes
# a certificate the renewal added; Cleanup removes the retained backups,
# the staged candidates and the transfer PKCS#12 of a published one.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HasePrivateNetworkRenewal.Common.ps1")

Assert-HaseRenewalComputer $ExpectedComputer

$running = @(Get-HaseRenewalRunningProcessNames)
if ($running.Count -ne 0) {
    throw "processes-running: stop the HASE host and clients first."
}

$journalFullPath = [System.IO.Path]::GetFullPath($JournalPath)
$journal = Read-HaseRenewalJson $journalFullPath
if (-not ($journal.computer -ceq $env:COMPUTERNAME)) {
    throw "journal-computer-mismatch: the journal belongs to another computer."
}
Format-HaseRenewalLine "Transaction" $journal.transactionId
Format-HaseRenewalLine "Journal phase" $journal.phase

foreach ($target in @($journal.targets)) {
    if ([System.IO.File]::Exists($target.path)) {
        $hash = Get-HaseRenewalFileSha256 $target.path
        if ($hash -ne $target.originalSha256 -and $hash -ne $target.candidateSha256) {
            throw "target-changed-outside-renewal: a target is neither its original nor its candidate."
        }
    }
}

if ($Mode -eq "Rollback") {
    if ($journal.phase -eq "rolled-back" -or $journal.phase -eq "cleaned") {
        throw "journal-phase-unexpected: nothing to roll back."
    }
    Undo-HaseRenewalTargets $journal
    Remove-HaseRenewalAddedCertificate $journal
    $journal.phase = "rolled-back"
    Save-HaseRenewalJournal $journalFullPath $journal
    Format-HaseRenewalLine "Rolled back" $true
    exit 0
}

if ($journal.phase -ne "published") {
    throw "journal-phase-unexpected: cleanup needs a published renewal."
}
foreach ($target in @($journal.targets)) {
    if ((Get-HaseRenewalFileSha256 $target.path) -ne $target.candidateSha256) {
        throw "target-not-candidate: a target is not its published candidate."
    }
}
$removed = 0
foreach ($target in @($journal.targets)) {
    foreach ($file in @($target.backupPath, $target.candidatePath)) {
        if ([System.IO.File]::Exists($file)) {
            [System.IO.File]::Delete($file)
            $removed++
        }
    }
}
$transfer = [System.IO.Path]::Combine(
    [System.IO.Path]::GetDirectoryName($journalFullPath),
    "Laptop",
    "laptop-client.pfx")
if ([System.IO.File]::Exists($transfer)) {
    [System.IO.File]::Delete($transfer)
    $removed++
}
$journal.phase = "cleaned"
Save-HaseRenewalJournal $journalFullPath $journal
Format-HaseRenewalLine "Files removed" $removed
Format-HaseRenewalLine "Cleaned" $true
exit 0
