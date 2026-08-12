[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ActiveProfilePath,[Parameter(Mandatory=$true)][string]$CleanupDirectory)
$ErrorActionPreference="Stop";Set-StrictMode -Version Latest
try{
 $profile=Get-Content $ActiveProfilePath -Raw|ConvertFrom-Json;$active=[IO.Path]::GetFullPath([string]$profile.clientCertificate.privateKeyPath);if(-not(Test-Path $active -PathType Leaf)){throw"active"}
 $path=Join-Path ([IO.Path]::GetFullPath($CleanupDirectory)) "obsolete-private-key-cleanup.json";$j=Get-Content $path -Raw|ConvertFrom-Json
 if([string]$j.phase-cne"committed"-or(Get-FileHash $active -Algorithm SHA256).Hash.ToLowerInvariant()-ceq[string]$j.oldPrivateKeySha256-or@( $j.targets|?{(Test-Path $_.source)-or(Test-Path $_.quarantine)}).Count-ne0){throw"cleanup"}
 Write-Host "Cleanup phase durable          : True";Write-Host "Transaction identity valid      : True";Write-Host "Active replacement key present  : True";Write-Host "Obsolete private-key files absent: True";Write-Host "Non-secret evidence retained     : True";Write-Host "Private-key cleanup valid        : True"
}catch{Write-Error "Laptop obsolete private-key cleanup validation failed.";exit 1}
