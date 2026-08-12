[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $EnrollmentPath,
    [Parameter(Mandatory = $true)] [string] $AuthorizationPolicyPath,
    [Parameter(Mandatory = $true)] [string] $ProvisioningDirectory,
    [Parameter(Mandatory = $true)] [string] $ExpectedTransactionId
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-HasePrivateCustodyFile([string] $Path)
{
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = Get-Acl -LiteralPath $Path
    $rules = @($acl.GetAccessRules($true, $true,
        [Security.Principal.SecurityIdentifier]))
    return $acl.Owner -eq $user.Value -and $rules.Count -ge 1 -and
        @($rules | Where-Object {
            $_.AccessControlType -ne
                [Security.AccessControl.AccessControlType]::Allow -or
            $_.IdentityReference -ne $user
        }).Count -eq 0
}

try
{
    $enrollment = [IO.Path]::GetFullPath($EnrollmentPath)
    $policy = [IO.Path]::GetFullPath($AuthorizationPolicyPath)
    $custody = [IO.Path]::GetFullPath($ProvisioningDirectory)
    $beginPath = Join-Path $custody `
        "cross-computer-rotation.transaction.json"
    $finalizationPath = Join-Path $custody `
        "cross-computer-rotation.finalization.json"
    $begin = Get-Content -LiteralPath $beginPath -Raw |
        ConvertFrom-Json -ErrorAction Stop
    $finalization = Get-Content -LiteralPath $finalizationPath -Raw |
        ConvertFrom-Json -ErrorAction Stop

    if ([string]$finalization.phase -cne "committed" -or
        [string]$finalization.transactionId -cne $ExpectedTransactionId -or
        [string]$begin.TransactionId -cne $ExpectedTransactionId -or
        -not [bool]$finalization.replacementConnectionProven)
    {
        throw "journal"
    }

    $enrollmentHash = (Get-FileHash -LiteralPath $enrollment `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    $policyHash = (Get-FileHash -LiteralPath $policy `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($enrollmentHash -cne [string]$begin.FinalSha256 -or
        $policyHash -cne [string]$begin.AuthorizationPolicySha256)
    {
        throw "revision"
    }

    $registry = Get-Content -LiteralPath $enrollment -Raw |
        ConvertFrom-Json -ErrorAction Stop
    $old = @($registry.enrollments | Where-Object {
        [string]$_.credentialId -ceq [string]$begin.CurrentCredentialId
    })
    $replacement = @($registry.enrollments | Where-Object {
        [string]$_.credentialId -ceq [string]$begin.ReplacementCredentialId
    })
    if ($old.Count -ne 0 -or $replacement.Count -ne 1 -or
        [string]$replacement[0].principalId -cne
            "hase-laptop-python-minipc")
    {
        throw "enrollment"
    }

    if (-not (Get-Acl -LiteralPath $custody).AreAccessRulesProtected)
    {
        throw "custody-root"
    }

    foreach ($path in @(
        $finalizationPath,
        [string]$finalization.overlapBackupPath,
        [string]$finalization.originalBackupPath))
    {
        if (-not (Test-Path -LiteralPath $path) -or
            -not (Test-HasePrivateCustodyFile $path))
        {
            throw "custody"
        }
    }

    Write-Host "Finalization phase durable     : True"
    Write-Host "Transaction identity exact     : True"
    Write-Host "Replacement proof recorded     : True"
    Write-Host "Old credential absent          : True"
    Write-Host "Replacement credential exact   : True"
    Write-Host "Principal unchanged             : True"
    Write-Host "Authorization byte-exact       : True"
    Write-Host "Overlap rollback retained      : True"
    Write-Host "Original Begin evidence retained: True"
    Write-Host "Protected custody valid        : True"
    Write-Host "MiniPC finalized               : True"
}
catch
{
    Write-Error "MiniPC cross-computer finalization validation failed."
    exit 1
}
