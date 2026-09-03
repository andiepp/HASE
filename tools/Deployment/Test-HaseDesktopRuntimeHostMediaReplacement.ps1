[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,
    [Parameter(Mandatory = $true)]
    [string]$CandidateSha256,
    [Parameter(Mandatory = $true)]
    [string]$ActiveMediaSha256,
    [ValidateRange(1, 16)]
    [int]$ExpectedCurrentSourceCount = 1,
    [ValidateRange(1, 16)]
    [int]$ExpectedReplacementSourceCount = 2,
    [bool]$ExpectedCurrentAudioConfigured = $false,
    [bool]$ExpectedReplacementAudioConfigured = $false,
    [string]$RepositoryPath = "H:\Development",

    [Parameter(Mandatory = $true)]
    [string]$ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaReplacement.Common.ps1")

if ($env:COMPUTERNAME -cne $ExpectedComputer) {
    throw "Run this tool only on $ExpectedComputer."
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
$plan = Get-HaseMediaReplacementPlan `
    -CandidatePath $CandidatePath `
    -ExpectedCandidateHash $CandidateSha256 `
    -ExpectedActiveMediaHash $ActiveMediaSha256 `
    -ExpectedCurrentSourceCount $ExpectedCurrentSourceCount `
    -ExpectedReplacementSourceCount $ExpectedReplacementSourceCount `
    -ExpectedCurrentAudioConfigured $ExpectedCurrentAudioConfigured `
    -ExpectedReplacementAudioConfigured $ExpectedReplacementAudioConfigured

Write-Host ""
Write-Host "ADR-0055 Runtime Host active-media replacement preflight"
Write-Host ""
Write-Host "Computer exact                 :" ($env:COMPUTERNAME -ceq $ExpectedComputer)
Write-Host "Repository commit exact        :" $true
Write-Host "Applications stopped           :" $true
Write-Host "Current source count           :" $plan.CurrentSourceCount
Write-Host "Replacement source count       :" $plan.ReplacementSourceCount
Write-Host "Current microphone configured  :" $plan.CurrentAudioConfigured
Write-Host "Replacement microphone configured:" `
    $plan.ReplacementAudioConfigured
Write-Host "Current media grant count      :" $plan.CurrentMediaGrantCount
Write-Host "Replacement media grant count  :" `
    $plan.ReplacementMediaGrantCount
Write-Host "Policy change required         :" $plan.PolicyChangeRequired
Write-Host "Profile unchanged              :" $true
Write-Host "Transaction ID                 :" $plan.TransactionId
Write-Host "Sensitive values withheld      :" $true
Write-Host "Preflight succeeded            :" $true
Write-Host ""
Write-Host "This preflight made no file, configuration, authorization, credential,"
Write-Host "application, device, signaling, deployment, recovery, serial, firmware,"
Write-Host "or physical change."
