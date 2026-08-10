[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProfilePath,[Parameter(Mandatory=$true)][string]$RollbackEvidencePath)
$ErrorActionPreference="Stop";Set-StrictMode -Version Latest
try{
 $profile=[IO.Path]::GetFullPath($ProfilePath);$rollback=[IO.Path]::GetFullPath($RollbackEvidencePath)
 if(-not(Test-Path -LiteralPath $profile -PathType Leaf)-or-not(Test-Path -LiteralPath $rollback -PathType Leaf)){throw"input"}
 $e=Get-Content -LiteralPath $rollback -Raw|ConvertFrom-Json;if($e.purpose-cne"hase-laptop-desktop-python-profile-custody-repair"-or-not[string]::Equals([string]$e.profilePath,$profile,[StringComparison]::OrdinalIgnoreCase)){throw"evidence"}
 $bytes=[Convert]::FromBase64String([string]$e.originalBase64);$sha=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace("-","").ToLowerInvariant()}finally{$sha.Dispose()};if($actual-cne[string]$e.originalSha256){throw"evidence"}
 [IO.File]::WriteAllBytes($profile,$bytes);$acl=New-Object Security.AccessControl.FileSecurity;$acl.SetSecurityDescriptorSddlForm([string]$e.originalSddl);Set-Acl -LiteralPath $profile -AclObject $acl
 Write-Host "Rollback evidence valid : True";Write-Host "Original profile restored: True"
}catch{Write-Error "Laptop Desktop Python profile custody restoration failed.";exit 1}
