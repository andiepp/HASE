[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProfilePath,
    [Parameter(Mandatory=$true)][string]$CertificatePath,
    [Parameter(Mandatory=$true)][string]$PrivateKeyPath,
    [Parameter(Mandatory=$true)][string]$DesktopServerCertificatePath,
    [Parameter(Mandatory=$true)][string]$MiniPcServerCertificatePath,
    [Parameter(Mandatory=$true)][string]$RollbackEvidencePath,
    [Parameter(Mandatory=$true)][string]$ExpectedComputer
)
$ErrorActionPreference="Stop"; Set-StrictMode -Version Latest
$profile=$null;$original=$null;$originalSddl=$null;$stage=$null;$published=$false
function Full([string]$p){if([string]::IsNullOrWhiteSpace($p)-or-not [IO.Path]::IsPathRooted($p)){throw"path"};[IO.Path]::GetFullPath($p)}
try{
 if($env:COMPUTERNAME -cne $ExpectedComputer){throw"machine"}
 $tool=Split-Path -Parent $MyInvocation.MyCommand.Path;$pkg=Split-Path -Parent $tool;$repo=[IO.Path]::GetFullPath((Join-Path $pkg "..\.."))
 if(@(& git -C $repo status --porcelain).Count-ne0){throw"repository"};$h=(& git -C $repo rev-parse HEAD).Trim();$o=(& git -C $repo rev-parse origin/main).Trim();if($h-ne$o){throw"repository"}
 $profile=Full $ProfilePath;$cert=Full $CertificatePath;$key=Full $PrivateKeyPath;$server=Full $DesktopServerCertificatePath;$mini=Full $MiniPcServerCertificatePath;$rollback=Full $RollbackEvidencePath
 foreach($p in @($profile,$cert,$key,$server,$mini)){if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw"input"}}
 if(-not(Test-Path -LiteralPath (Split-Path -Parent $rollback) -PathType Container)){throw"rollback"}
 $original=[IO.File]::ReadAllBytes($profile);$doc=Get-Content -LiteralPath $profile -Raw|ConvertFrom-Json
 $oldCert="C:\Users\aeppi\AppData\Local\HASE\PythonAutomation\Security\python-client-chain.pem";$oldKey="C:\Users\aeppi\AppData\Local\HASE\PythonAutomation\Security\python-client-key.pem";$oldServer="C:\Users\aeppi\AppData\Local\HASE\PrivateNetworkValidation\runtime-host-server.cer"
 if(-not[string]::Equals([string]$doc.clientCertificate.certificateChainPath,$oldCert,[StringComparison]::OrdinalIgnoreCase)-or-not[string]::Equals([string]$doc.clientCertificate.privateKeyPath,$oldKey,[StringComparison]::OrdinalIgnoreCase)-or-not[string]::Equals([string]$doc.trustedServerCertificate.certificatePath,$oldServer,[StringComparison]::OrdinalIgnoreCase)){throw"stale-shape"}
 $python=Join-Path $pkg ".venv\Scripts\python.exe";&$python -c "import ssl,sys;c=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT);c.load_cert_chain(sys.argv[1],sys.argv[2])" $cert $key;if($LASTEXITCODE-ne0){throw"credential"}
 if((Get-FileHash -LiteralPath $server -Algorithm SHA256).Hash-eq(Get-FileHash -LiteralPath $mini -Algorithm SHA256).Hash){throw"server-certificates"}
 $hash=(Get-FileHash -LiteralPath $profile -Algorithm SHA256).Hash.ToLowerInvariant();$originalSddl=(Get-Acl -LiteralPath $profile).Sddl
 $e=[ordered]@{schemaVersion=1;purpose="hase-laptop-desktop-python-profile-custody-repair";profilePath=$profile;originalSha256=$hash;originalSddl=$originalSddl;originalBase64=[Convert]::ToBase64String($original)}
 $utf8=[Text.UTF8Encoding]::new($false)
 if(Test-Path -LiteralPath $rollback -PathType Leaf){
  $existing=Get-Content -LiteralPath $rollback -Raw|ConvertFrom-Json
  $existingBytes=[Convert]::FromBase64String([string]$existing.originalBase64)
  $sha=[Security.Cryptography.SHA256]::Create();try{$existingHash=[BitConverter]::ToString($sha.ComputeHash($existingBytes)).Replace("-","").ToLowerInvariant()}finally{$sha.Dispose()}
  $existingBase64=[Convert]::ToBase64String($existingBytes);$originalBase64=[Convert]::ToBase64String($original)
  if($existing.purpose -cne $e.purpose -or -not [string]::Equals([string]$existing.profilePath,$profile,[StringComparison]::OrdinalIgnoreCase) -or $existingHash -cne $hash -or [string]$existing.originalSha256 -cne $hash -or [string]$existing.originalSddl -cne $originalSddl -or $existingBase64 -cne $originalBase64){throw"rollback"}
 }elseif(Test-Path -LiteralPath $rollback){throw"rollback"}else{[IO.File]::WriteAllText($rollback,($e|ConvertTo-Json -Depth 4),$utf8)}
 $doc.clientCertificate.certificateChainPath=$cert;$doc.clientCertificate.privateKeyPath=$key;$doc.trustedServerCertificate.certificatePath=$server
 $candidate=($doc|ConvertTo-Json -Depth 8);$candidateBytes=$utf8.GetBytes($candidate);[IO.File]::WriteAllBytes($profile,$candidateBytes);$published=$true
 if((Get-Acl -LiteralPath $profile).Sddl-cne$originalSddl){throw"acl"}
 &$python -c "from hase import load_runtime_host_profile;import sys;load_runtime_host_profile(sys.argv[1])" $profile;if($LASTEXITCODE-ne0){throw"verification"}
 $verified=Get-Content -LiteralPath $profile -Raw|ConvertFrom-Json
 if(-not[string]::Equals([string]$verified.clientCertificate.certificateChainPath,$cert,[StringComparison]::OrdinalIgnoreCase)-or-not[string]::Equals([string]$verified.clientCertificate.privateKeyPath,$key,[StringComparison]::OrdinalIgnoreCase)-or-not[string]::Equals([string]$verified.trustedServerCertificate.certificatePath,$server,[StringComparison]::OrdinalIgnoreCase)){throw"verification"}
 Write-Host "Laptop machine exact       : True";Write-Host "Stale custody recognized   : True";Write-Host "Certificate key pair valid : True";Write-Host "Server certificates distinct: True";Write-Host "Rollback evidence recorded : True";Write-Host "Profile custody corrected  : True"
}catch{if($published -and $null -ne $original){[IO.File]::WriteAllBytes($profile,$original);$acl=New-Object Security.AccessControl.FileSecurity;$acl.SetSecurityDescriptorSddlForm($originalSddl);Set-Acl -LiteralPath $profile -AclObject $acl};if($null -ne $stage -and (Test-Path -LiteralPath $stage)){Remove-Item -LiteralPath $stage -Force};Write-Error "Laptop Desktop Python profile custody repair failed.";exit 1}
