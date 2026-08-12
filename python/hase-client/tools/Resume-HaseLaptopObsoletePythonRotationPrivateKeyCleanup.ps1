[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$CleanupDirectory)
$ErrorActionPreference="Stop";Set-StrictMode -Version Latest
try{
 if($env:COMPUTERNAME-cne"LTAEP"-or@(Get-Process Hase.DesktopHost.App,Hase.Client.Wpf.App -ErrorAction SilentlyContinue).Count-ne0){throw"preflight"}
 $root=[IO.Path]::GetFullPath($CleanupDirectory);$path=Join-Path $root "obsolete-private-key-cleanup.json";$j=Get-Content $path -Raw|ConvertFrom-Json
 if([string]$j.purpose-cne"hase-laptop-python-rotation-obsolete-private-key-cleanup"-or[string]$j.phase-ceq"committed"){throw"journal"}
 foreach($t in @($j.targets)){if(Test-Path $t.source){Move-Item $t.source $t.quarantine};if(Test-Path $t.quarantine){if((Get-FileHash $t.quarantine -Algorithm SHA256).Hash.ToLowerInvariant()-cne[string]$j.oldPrivateKeySha256){throw"hash"};Remove-Item $t.quarantine -Force}}
 $j.phase="committed";[IO.File]::WriteAllText($path,($j|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
 Write-Host "Interrupted cleanup resumed : True";Write-Host "Obsolete private keys absent: True";Write-Host "Cleanup committed           : True"
}catch{Write-Error "Laptop obsolete private-key cleanup recovery failed.";exit 1}
