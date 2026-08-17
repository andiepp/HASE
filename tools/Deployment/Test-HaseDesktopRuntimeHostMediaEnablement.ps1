[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,
    [Parameter(Mandatory = $true)]
    [string]$CandidateSha256,
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationRequestPath,
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationRequestSha256,
    [string]$RepositoryPath = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

if ($env:COMPUTERNAME -cne "AEPRAKETE") {
    throw "Run this tool only on AEPRAKETE."
}
foreach ($hash in @($CandidateSha256, $AuthorizationRequestSha256)) {
    if ($hash -cnotmatch '^[0-9A-Fa-f]{64}$') {
        throw "Every expected SHA-256 must contain exactly sixty-four hexadecimal characters."
    }
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
$plan = Get-HaseMediaEnablementPlan `
    -CandidatePath $CandidatePath `
    -AuthorizationRequestPath $AuthorizationRequestPath `
    -ExpectedCandidateHash $CandidateSha256 `
    -ExpectedAuthorizationRequestHash $AuthorizationRequestSha256

Write-Host ""
Write-Host "ADR-0055 Runtime Host media enablement preflight"
Write-Host ""
Write-Host "Computer exact                 :" ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Repository commit exact        :" $true
Write-Host "Applications stopped           :" $true
Write-Host "Client credential match unique :" $true
Write-Host "Enrolled principal match unique:" $true
Write-Host "Configured source count        :" $plan.SourceCount
Write-Host "Microphone configured          :" $plan.AudioConfigured
Write-Host "New media grant count          :" $plan.Permissions.Count
Write-Host "Transaction ID                 :" $plan.TransactionId
Write-Host "Sensitive values withheld      :" $true
Write-Host "Preflight succeeded            :" $true
Write-Host ""
Write-Host "This preflight made no file, configuration, authorization, credential,"
Write-Host "application, device, signaling, deployment, serial, firmware, or"
Write-Host "physical change."
