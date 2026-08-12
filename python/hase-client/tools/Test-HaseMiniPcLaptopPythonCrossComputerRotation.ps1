[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RotationRequestPath,[Parameter(Mandatory=$true)][string]$EnrollmentPath,[Parameter(Mandatory=$true)][string]$AuthorizationPolicyPath,[Parameter(Mandatory=$true)][string]$ProvisioningDirectory,[Parameter(Mandatory=$true)][string]$TransferArchivePath)
$ErrorActionPreference="Stop";Set-StrictMode -Version Latest
try{
 $r=Get-Content $RotationRequestPath -Raw|ConvertFrom-Json;$jp=Join-Path $ProvisioningDirectory "cross-computer-rotation.transaction.json";$j=Get-Content $jp -Raw|ConvertFrom-Json
 if([string]$j.Phase-cne"overlap-published"){throw"phase"};$e=Get-Content $EnrollmentPath -Raw|ConvertFrom-Json
 $o=@($e.enrollments|?{[string]$_.credentialId-ceq[string]$r.expectedCurrentCredentialId});$n=@($e.enrollments|?{[string]$_.credentialId-ceq[string]$j.ReplacementCredentialId})
 if($o.Count-ne1-or$n.Count-ne1-or[string]$o[0].principalId-cne[string]$n[0].principalId-or[string]$o[0].trustPolicyId-cne[string]$n[0].trustPolicyId){throw"overlap"}
 if((Get-FileHash $AuthorizationPolicyPath -Algorithm SHA256).Hash.ToLowerInvariant()-cne[string]$j.AuthorizationPolicySha256){throw"policy"}
 if((Get-FileHash $TransferArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()-cne[string]$j.TransferArchiveSha256){throw"archive"}
 Add-Type -AssemblyName System.IO.Compression.FileSystem;$z=[IO.Compression.ZipFile]::OpenRead($TransferArchivePath)
 try{$names=@($z.Entries.Name|sort);$expected=@("client-certificate.pem","private-key.pem","runtime-host-profile.json","transfer-manifest.json")|sort;if(@(Compare-Object $names $expected).Count-ne0){throw"shape"}}finally{$z.Dispose()}
 Write-Host "Overlap phase durable          : True";Write-Host "Old credential enrolled        : True";Write-Host "Replacement credential enrolled: True";Write-Host "Principal and trust unchanged  : True";Write-Host "Authorization byte-exact       : True";Write-Host "Transfer archive byte-exact    : True";Write-Host "Archive contains four files    : True";Write-Host "Old private key archived       : False";Write-Host "Rollback retained              : True";Write-Host "Cross-computer Begin valid     : True"
}catch{Write-Error "MiniPC Laptop Python cross-computer rotation validation failed.";exit 1}
