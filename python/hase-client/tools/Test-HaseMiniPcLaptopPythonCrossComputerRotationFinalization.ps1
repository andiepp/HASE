[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $EnrollmentPath,
    [Parameter(Mandatory = $true)] [string] $AuthorizationPolicyPath,
    [Parameter(Mandatory = $true)] [string] $ProvisioningDirectory,
    [Parameter(Mandatory = $true)] [string] $ExpectedTransactionId
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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

    foreach ($path in @(
        $custody,
        $finalizationPath,
        [string]$finalization.overlapBackupPath,
        [string]$finalization.originalBackupPath))
    {
        if (-not (Test-Path -LiteralPath $path) -or
            -not (Get-Acl -LiteralPath $path).AreAccessRulesProtected)
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
