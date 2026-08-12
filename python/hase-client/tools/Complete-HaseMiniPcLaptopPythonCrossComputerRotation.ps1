[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $RotationRequestPath,
    [Parameter(Mandatory = $true)] [string] $EnrollmentPath,
    [Parameter(Mandatory = $true)] [string] $AuthorizationPolicyPath,
    [Parameter(Mandatory = $true)] [string] $ProvisioningDirectory,
    [Parameter(Mandatory = $true)] [string] $ExpectedTransactionId,
    [Parameter(Mandatory = $true)] [switch] $ReplacementConnectionProven
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-HaseAbsolutePath([string] $Value)
{
    if ([string]::IsNullOrWhiteSpace($Value) -or
        $Value -cne $Value.Trim() -or
        $Value -notmatch '^[A-Za-z]:[\\/]')
    {
        throw "path"
    }

    [IO.Path]::GetFullPath($Value)
}

function Test-HaseReparsePointInExistingChain([string] $Path)
{
    $current = [IO.Path]::GetFullPath($Path)
    while ($null -ne $current)
    {
        if (Test-Path -LiteralPath $current)
        {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band
                [IO.FileAttributes]::ReparsePoint)
            {
                return $true
            }
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrEmpty($parent) -or $parent -ceq $current)
        {
            break
        }

        $current = $parent
    }

    return $false
}

function Get-HaseSha256([string] $Path)
{
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).
        Hash.ToLowerInvariant()
}

function Get-HaseAccessSddl([string] $Path)
{
    (Get-Acl -LiteralPath $Path).GetSecurityDescriptorSddlForm(
        [Security.AccessControl.AccessControlSections]::Access)
}

function Test-HasePrivateCustodyFile([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = Get-Acl -LiteralPath $Path
    $owner = [Security.Principal.NTAccount]::new($acl.Owner).
        Translate([Security.Principal.SecurityIdentifier])
    $rules = @($acl.GetAccessRules($true, $true,
        [Security.Principal.SecurityIdentifier]))
    $invalidRules = @($rules | Where-Object {
        $_.AccessControlType -ne
            [Security.AccessControl.AccessControlType]::Allow -or
        $_.IdentityReference -ne $user
    })
    return $owner -eq $user -and
        $rules.Count -ge 1 -and $invalidRules.Count -eq 0
}

function Write-HaseUtf8Json([string] $Path, [object] $Document)
{
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
        ($Document | ConvertTo-Json -Depth 12))
    try
    {
        [IO.File]::WriteAllBytes($Path, $bytes)
    }
    finally
    {
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}

function Write-HaseExistingBytes([string] $Path, [byte[]] $Bytes)
{
    $stream = [IO.File]::Open(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None)
    try
    {
        $stream.SetLength(0)
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush($true)
    }
    finally
    {
        $stream.Dispose()
    }
}

$phase = "preflight"
$finalBytes = $null
$overlapBytes = $null

try
{
    if ($env:OS -cne "Windows_NT" -or
        $env:COMPUTERNAME -cne "LABC")
    {
        throw "machine"
    }

    if (-not $ReplacementConnectionProven)
    {
        throw "replacement-connection-proof"
    }

    if ($ExpectedTransactionId -notmatch '^[0-9a-f]{32}$')
    {
        throw "transaction"
    }

    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repositoryRoot = [IO.Path]::GetFullPath(
        (Join-Path $toolDirectory "..\..\.."))
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    $status = @(& git -C $repositoryRoot status --porcelain)

    if ($head -cne $origin -or $status.Count -ne 0)
    {
        throw "repository"
    }

    if (@(
        Get-Process -Name "Hase.DesktopHost.App","Hase.Client.Wpf.App" `
            -ErrorAction SilentlyContinue
    ).Count -ne 0)
    {
        throw "processes"
    }

    $requestPath = Resolve-HaseAbsolutePath $RotationRequestPath
    $enrollment = Resolve-HaseAbsolutePath $EnrollmentPath
    $policy = Resolve-HaseAbsolutePath $AuthorizationPolicyPath
    $custody = Resolve-HaseAbsolutePath $ProvisioningDirectory
    $beginJournalPath = Join-Path $custody `
        "cross-computer-rotation.transaction.json"
    $finalizationJournalPath = Join-Path $custody `
        "cross-computer-rotation.finalization.json"

    foreach ($path in @(
        $requestPath,
        $enrollment,
        $policy,
        $beginJournalPath))
    {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Test-HaseReparsePointInExistingChain $path))
        {
            throw "input"
        }
    }

    if (-not (Test-Path -LiteralPath $custody -PathType Container) -or
        (Test-HaseReparsePointInExistingChain $custody) -or
        -not (Get-Acl -LiteralPath $custody).AreAccessRulesProtected)
    {
        throw "custody"
    }

    if (Test-Path -LiteralPath $finalizationJournalPath)
    {
        throw "finalization-recovery-required"
    }

    $request = Get-Content -LiteralPath $requestPath -Raw |
        ConvertFrom-Json -ErrorAction Stop
    $begin = Get-Content -LiteralPath $beginJournalPath -Raw |
        ConvertFrom-Json -ErrorAction Stop

    if ([int]$request.schemaVersion -ne 1 -or
        [string]$request.purpose -cne
            "hase-laptop-minipc-python-cross-computer-rotation-request" -or
        [string]$request.targetId -cne "minipc-runtime-host" -or
        [string]$request.principalId -cne "hase-laptop-python-minipc" -or
        [string]$begin.Phase -cne "overlap-published" -or
        [string]$begin.TransactionId -cne $ExpectedTransactionId -or
        [string]$begin.CurrentCredentialId -cne
            [string]$request.expectedCurrentCredentialId)
    {
        throw "evidence"
    }

    $journalEnrollment = Resolve-HaseAbsolutePath `
        ([string]$begin.EnrollmentPath)
    $journalPolicy = Resolve-HaseAbsolutePath `
        ([string]$begin.AuthorizationPolicyPath)
    $finalEnrollmentPath = Resolve-HaseAbsolutePath `
        ([string]$begin.FinalEnrollmentPath)
    $originalBackupPath = Resolve-HaseAbsolutePath `
        ([string]$begin.EnrollmentBackupPath)

    if (-not [string]::Equals(
            $journalEnrollment,
            $enrollment,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            $journalPolicy,
            $policy,
            [StringComparison]::OrdinalIgnoreCase))
    {
        throw "path-binding"
    }

    foreach ($path in @($finalEnrollmentPath, $originalBackupPath))
    {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Test-HaseReparsePointInExistingChain $path))
        {
            throw "retained-input"
        }
    }

    if ((Get-HaseSha256 $enrollment) -cne [string]$begin.OverlapSha256 -or
        (Get-HaseSha256 $finalEnrollmentPath) -cne
            [string]$begin.FinalSha256 -or
        (Get-HaseSha256 $policy) -cne
            [string]$begin.AuthorizationPolicySha256)
    {
        throw "revision"
    }

    $overlap = Get-Content -LiteralPath $enrollment -Raw |
        ConvertFrom-Json -ErrorAction Stop
    $final = Get-Content -LiteralPath $finalEnrollmentPath -Raw |
        ConvertFrom-Json -ErrorAction Stop
    $oldId = [string]$begin.CurrentCredentialId
    $replacementId = [string]$begin.ReplacementCredentialId
    $overlapOld = @($overlap.enrollments | Where-Object {
        [string]$_.credentialId -ceq $oldId
    })
    $overlapReplacement = @($overlap.enrollments | Where-Object {
        [string]$_.credentialId -ceq $replacementId
    })
    $finalOld = @($final.enrollments | Where-Object {
        [string]$_.credentialId -ceq $oldId
    })
    $finalReplacement = @($final.enrollments | Where-Object {
        [string]$_.credentialId -ceq $replacementId
    })

    if ($overlapOld.Count -ne 1 -or
        $overlapReplacement.Count -ne 1 -or
        $finalOld.Count -ne 0 -or
        $finalReplacement.Count -ne 1 -or
        [string]$overlapOld[0].principalId -cne
            [string]$overlapReplacement[0].principalId -or
        [string]$overlapOld[0].trustPolicyId -cne
            [string]$overlapReplacement[0].trustPolicyId -or
        [string]$finalReplacement[0].principalId -cne
            [string]$overlapReplacement[0].principalId -or
        [string]$finalReplacement[0].trustPolicyId -cne
            [string]$overlapReplacement[0].trustPolicyId)
    {
        throw "transition"
    }

    $phase = "preparation"
    $overlapBackupPath = Join-Path $custody `
        "enrollment.overlap-before-finalization.json"
    if (Test-Path -LiteralPath $overlapBackupPath)
    {
        throw "finalization-output"
    }

    $overlapBytes = [IO.File]::ReadAllBytes($enrollment)
    $finalBytes = [IO.File]::ReadAllBytes($finalEnrollmentPath)
    [IO.File]::WriteAllBytes($overlapBackupPath, $overlapBytes)
    if (-not (Test-HasePrivateCustodyFile $overlapBackupPath))
    {
        throw "backup-custody"
    }

    $enrollmentAccessSddl = Get-HaseAccessSddl $enrollment
    $journal = [ordered]@{
        schemaVersion = 1
        purpose =
            "hase-minipc-laptop-python-cross-computer-rotation-finalization"
        transactionId = $ExpectedTransactionId
        phase = "prepared"
        replacementConnectionProven = $true
        currentCredentialId = $oldId
        replacementCredentialId = $replacementId
        overlapEnrollmentSha256 = [string]$begin.OverlapSha256
        finalEnrollmentSha256 = [string]$begin.FinalSha256
        authorizationPolicySha256 =
            [string]$begin.AuthorizationPolicySha256
        overlapBackupPath = $overlapBackupPath
        originalBackupPath = $originalBackupPath
    }
    Write-HaseUtf8Json $finalizationJournalPath $journal
    if (-not (Test-HasePrivateCustodyFile $finalizationJournalPath))
    {
        throw "journal-custody"
    }

    $phase = "final-enrollment-publication"
    Write-HaseExistingBytes $enrollment $finalBytes

    if ((Get-HaseSha256 $enrollment) -cne [string]$begin.FinalSha256 -or
        (Get-HaseAccessSddl $enrollment) -cne $enrollmentAccessSddl)
    {
        throw "final-enrollment-verification"
    }

    $journal.phase = "committed"
    Write-HaseUtf8Json $finalizationJournalPath $journal

    if (-not (Test-HasePrivateCustodyFile $finalizationJournalPath) -or
        (Get-HaseSha256 $policy) -cne
            [string]$begin.AuthorizationPolicySha256)
    {
        throw "committed-verification"
    }

    Write-Host "Operation                    : Finalize cross-computer credential rotation"
    Write-Host "Outcome                      : Succeeded"
    Write-Host "Transaction exact            : True"
    Write-Host "Replacement connection proven: True"
    Write-Host "Old credential revoked       : True"
    Write-Host "Replacement credential active: True"
    Write-Host "Authorization byte-exact     : True"
    Write-Host "Overlap rollback retained    : True"
    Write-Host "Original evidence retained   : True"
    Write-Host "MiniPC finalization ready    : True"
}
catch
{
    $primaryFailureType = $_.Exception.GetType().FullName
    $primaryFailureHResult = $_.Exception.HResult
    Write-Error (
        "MiniPC cross-computer finalization failed at phase " +
        "'$phase'. Primary: $primaryFailureType/$primaryFailureHResult.")
    exit 1
}
finally
{
    if ($null -ne $finalBytes)
    {
        [Array]::Clear($finalBytes, 0, $finalBytes.Length)
    }

    if ($null -ne $overlapBytes)
    {
        [Array]::Clear($overlapBytes, 0, $overlapBytes.Length)
    }
}
