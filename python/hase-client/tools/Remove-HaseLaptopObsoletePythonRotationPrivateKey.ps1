[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $RotationRequestPath,
    [Parameter(Mandatory = $true)] [string] $ActiveProfilePath,
    [Parameter(Mandatory = $true)] [string[]] $CutoverCustodyDirectories,
    [Parameter(Mandatory = $true)] [string] $CleanupDirectory,
    [Parameter(Mandatory = $true)] [string] $ExpectedTransactionId,
    [Parameter(Mandatory = $true)] [switch] $ReplacementOnlyConnectionProven,
    [Parameter(Mandatory = $true)] [string] $ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Full([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or
        $Value -cne $Value.Trim() -or
        $Value -notmatch '^[A-Za-z]:[\\/]') { throw "path" }
    [IO.Path]::GetFullPath($Value)
}

function Hash([string] $Path)
{
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function PrivateFile([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = Get-Acl -LiteralPath $Path
    $owner = [Security.Principal.NTAccount]::new($acl.Owner).
        Translate([Security.Principal.SecurityIdentifier])
    $rules = @($acl.GetAccessRules($true, $true,
        [Security.Principal.SecurityIdentifier]))
    return $owner -eq $user -and $rules.Count -ge 1 -and
        @($rules | Where-Object {
            $_.AccessControlType -ne
                [Security.AccessControl.AccessControlType]::Allow -or
            $_.IdentityReference -ne $user
        }).Count -eq 0
}

function WriteJson([string] $Path, [object] $Document)
{
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
        ($Document | ConvertTo-Json -Depth 12))
    try { [IO.File]::WriteAllBytes($Path, $bytes) }
    finally { [Array]::Clear($bytes, 0, $bytes.Length) }
}

$phase = "preflight"
try
{
    if ($env:OS -cne "Windows_NT" -or $env:COMPUTERNAME -cne $ExpectedComputer -or
        -not $ReplacementOnlyConnectionProven -or
        $ExpectedTransactionId -notmatch '^[0-9a-f]{32}$') { throw "boundary" }
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $root = [IO.Path]::GetFullPath((Join-Path $toolDirectory "..\..\.."))
    if ((& git -C $root rev-parse HEAD).Trim() -cne
            (& git -C $root rev-parse origin/main).Trim() -or
        @(& git -C $root status --porcelain).Count -ne 0 -or
        @(Get-Process -Name "Hase.DesktopHost.App","Hase.Client.Wpf.App" `
            -ErrorAction SilentlyContinue).Count -ne 0) { throw "preflight" }

    $requestPath = Full $RotationRequestPath
    $profilePath = Full $ActiveProfilePath
    $cleanup = Full $CleanupDirectory
    if (-not (Test-Path $requestPath -PathType Leaf) -or
        -not (Test-Path $profilePath -PathType Leaf) -or
        (Test-Path $cleanup)) { throw "input" }
    $request = Get-Content $requestPath -Raw | ConvertFrom-Json -ErrorAction Stop
    $profile = Get-Content $profilePath -Raw | ConvertFrom-Json -ErrorAction Stop
    if ([string]$request.purpose -cne
            "hase-laptop-minipc-python-cross-computer-rotation-request" -or
        [string]$request.principalId -cne "hase-laptop-python-minipc" -or
        [string]$request.privateKeySha256 -notmatch '^[0-9a-f]{64}$')
    { throw "request" }
    $oldHash = [string]$request.privateKeySha256
    $activeKey = Full ([string]$profile.clientCertificate.privateKeyPath)
    if (-not (Test-Path $activeKey -PathType Leaf) -or
        (Hash $activeKey) -ceq $oldHash) { throw "active-key" }

    $directories = @($CutoverCustodyDirectories | ForEach-Object { Full $_ })
    if ($directories.Count -lt 1 -or
        @($directories | Sort-Object -Unique).Count -ne $directories.Count)
    { throw "custody-list" }
    $targets = @()
    foreach ($directory in $directories)
    {
        $journalPath = Join-Path $directory "laptop-cutover.transaction.json"
        $keyPath = Join-Path $directory "rollback\private-key.pem"
        if (-not (Test-Path $journalPath -PathType Leaf) -or
            -not (Test-Path $keyPath -PathType Leaf) -or
            -not (PrivateFile $journalPath) -or -not (PrivateFile $keyPath))
        { throw "custody" }
        $journal = Get-Content $journalPath -Raw | ConvertFrom-Json -ErrorAction Stop
        if ([string]$journal.transactionId -cne $ExpectedTransactionId -or
            [string]$journal.currentCredentialId -cne
                [string]$request.expectedCurrentCredentialId -or
            (Hash $keyPath) -cne $oldHash) { throw "target-binding" }
        $targets += [pscustomobject]@{ Source = $keyPath
            Name = (Split-Path -Leaf $directory) + ".private-key.pem" }
    }

    $phase = "preparation"
    $parent = Split-Path -Parent $cleanup
    if (-not (Test-Path $parent -PathType Container) -or
        -not (Get-Acl $parent).AreAccessRulesProtected) { throw "cleanup-parent" }
    [IO.Directory]::CreateDirectory($cleanup) | Out-Null
    if (-not (PrivateFile $cleanup)) { throw "cleanup-custody" }
    $journalPath = Join-Path $cleanup "obsolete-private-key-cleanup.json"
    $journal = [ordered]@{ schemaVersion = 1
        purpose = "hase-laptop-python-rotation-obsolete-private-key-cleanup"
        transactionId = $ExpectedTransactionId; phase = "prepared"
        oldPrivateKeySha256 = $oldHash; targetCount = $targets.Count
        targets = @($targets | ForEach-Object { [ordered]@{
            source = $_.Source; quarantine = (Join-Path $cleanup $_.Name) } }) }
    WriteJson $journalPath $journal
    if (-not (PrivateFile $journalPath)) { throw "journal-custody" }

    $phase = "quarantine"
    foreach ($target in $journal.targets)
    {
        Move-Item -LiteralPath $target.source -Destination $target.quarantine
        if ((Hash $target.quarantine) -cne $oldHash -or
            -not (PrivateFile $target.quarantine)) { throw "quarantine" }
    }
    $journal.phase = "quarantined"; WriteJson $journalPath $journal

    $phase = "deletion"
    foreach ($target in $journal.targets)
    { Remove-Item -LiteralPath $target.quarantine -Force }
    $journal.phase = "committed"; WriteJson $journalPath $journal
    if (@($journal.targets | Where-Object {
        (Test-Path $_.source) -or (Test-Path $_.quarantine) }).Count -ne 0)
    { throw "committed-verification" }

    Write-Host "Operation                       : Remove obsolete rotation private keys"
    Write-Host "Outcome                         : Succeeded"
    Write-Host "Transaction exact               : True"
    Write-Host "Replacement-only proof required : True"
    Write-Host "Active replacement key unchanged: True"
    Write-Host "Old-key targets exact           : True"
    Write-Host "Obsolete private keys absent    : True"
    Write-Host "Cleanup journal durable         : True"
    Write-Host "Non-secret evidence retained    : True"
}
catch
{
    Write-Error "Laptop obsolete private-key cleanup failed at phase '$phase'."
    exit 1
}
