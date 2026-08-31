<#
.SYNOPSIS
    Fast-forwards this computer's HASE repository to origin/main.

.DESCRIPTION
    Run this on each computer that carries a HASE working tree, one at a
    time, from inside the repository. It fetches origin/main and
    fast-forwards to it.

    It refuses rather than repairs. A working tree that is not clean, a
    branch other than main, and a branch that has diverged from origin all
    stop the script with the state left untouched, because each of those
    means something happened on this computer that the operator needs to
    look at before history moves.

    Synchronizing the repository does not refresh an installed
    application. Desktop shortcuts start the published installation, not
    the repository build, so an installed Client or Runtime Host stays on
    its previous build until it is republished on that computer.

.PARAMETER ExpectedCommit
    The commit this computer is expected to reach. It is resolved through
    Git, so an abbreviation or any other revision Git understands is
    accepted. The script stops if the result does not match, and stops if
    the value names no commit at all. Omit it to fast-forward to whatever
    origin/main currently holds.

.EXAMPLE
    .\Sync-HaseRepository.ps1

.EXAMPLE
    .\Sync-HaseRepository.ps1 -ExpectedCommit f154c5a
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedCommit
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = & git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "This is not a Git repository working directory."
}

Write-Output "computer     : $env:COMPUTERNAME"
Write-Output "repository   : $repositoryRoot"

$branch = & git rev-parse --abbrev-ref HEAD
if ($LASTEXITCODE -ne 0) {
    throw "The current branch could not be determined."
}

if ($branch -ne "main") {
    throw "The current branch is '$branch'; this script only synchronizes 'main'."
}

# A failed status reports no entries, which would read as a clean tree.
$beforeEntries = @(& git status --porcelain | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) {
    throw "The working-tree status could not be determined."
}

if ($beforeEntries.Count -ne 0) {
    Write-Output "working tree is not clean:"
    foreach ($entry in $beforeEntries) {
        Write-Output "  $entry"
    }

    throw "Refusing to fast-forward a working tree that carries changes."
}

$headBefore = & git rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw "The current commit could not be determined."
}

& git fetch origin main
if ($LASTEXITCODE -ne 0) {
    throw "Fetching origin/main failed."
}

& git merge --ff-only origin/main
if ($LASTEXITCODE -ne 0) {
    throw "Fast-forwarding to origin/main failed; the branch has diverged."
}

$headAfter = & git rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw "The resulting commit could not be determined."
}

$originCommit = & git rev-parse origin/main
if ($LASTEXITCODE -ne 0) {
    throw "The origin/main commit could not be determined."
}

$afterEntries = @(& git status --porcelain | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) {
    throw "The resulting working-tree status could not be determined."
}

$isClean = $afterEntries.Count -eq 0
$isLevel = $headAfter -eq $originCommit
$didAdvance = $headBefore -ne $headAfter

Write-Output ""
Write-Output "commit before: $headBefore"
Write-Output "HEAD         : $headAfter"
Write-Output "origin/main  : $originCommit"
Write-Output "advanced     : $didAdvance"
Write-Output "clean        : $isClean"
Write-Output "level        : $isLevel"

if ($PSBoundParameters.ContainsKey("ExpectedCommit")) {
    # Resolve rather than compare as text, so that an abbreviation, a tag
    # or any other revision Git understands is accepted. Comparing the
    # forty-character commit against what an operator actually types
    # rejects correct input.
    $resolvedExpected = & git rev-parse --verify --quiet "$ExpectedCommit^{commit}"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolvedExpected)) {
        throw "'$ExpectedCommit' does not name a commit in this repository."
    }

    $isExpected = $headAfter -eq $resolvedExpected
    Write-Output "expected     : $resolvedExpected"
    Write-Output "at expected  : $isExpected"

    if (-not $isExpected) {
        throw "HEAD is $headAfter but $resolvedExpected was expected."
    }
}

if (-not $isClean) {
    throw "The working tree is not clean after the fast-forward."
}

if (-not $isLevel) {
    throw "HEAD is not level with origin/main after the fast-forward."
}

Write-Output ""
Write-Output "Repository synchronized. Installed applications are unchanged."
