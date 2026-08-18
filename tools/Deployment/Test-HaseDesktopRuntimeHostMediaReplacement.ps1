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
    [string]$RepositoryPath = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaReplacement.Common.ps1")

if ($env:COMPUTERNAME -cne "AEPRAKETE") {
    throw "Run this tool only on AEPRAKETE."
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
$plan = Get-HaseMediaReplacementPlan `
    -CandidatePath $CandidatePath `
    -ExpectedCandidateHash $CandidateSha256 `
    -ExpectedActiveMediaHash $ActiveMediaSha256 `
    -ExpectedCurrentSourceCount $ExpectedCurrentSourceCount `
    -ExpectedReplacementSourceCount $ExpectedReplacementSourceCount `
    -ExpectedAudioConfigured $false

Write-Host ""
Write-Host "ADR-0055 Runtime Host active-media replacement preflight"
Write-Host ""
Write-Host "Computer exact                 :" ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Repository commit exact        :" $true
Write-Host "Applications stopped           :" $true
Write-Host "Current source count           :" $plan.CurrentSourceCount
Write-Host "Replacement source count       :" $plan.ReplacementSourceCount
Write-Host "Microphone configured          :" $plan.AudioConfigured
Write-Host "Existing media grant count     :" $plan.MediaGrantCount
Write-Host "Profile and policy unchanged   :" $true
Write-Host "Transaction ID                 :" $plan.TransactionId
Write-Host "Sensitive values withheld      :" $true
Write-Host "Preflight succeeded            :" $true
Write-Host ""
Write-Host "This preflight made no file, configuration, authorization, credential,"
Write-Host "application, device, signaling, deployment, recovery, serial, firmware,"
Write-Host "or physical change."
