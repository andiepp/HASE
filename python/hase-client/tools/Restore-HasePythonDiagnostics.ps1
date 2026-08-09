[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$AuthorizationPolicyPath,
 [Parameter(Mandatory=$true)][string]$ApplicationProfilePath,
 [Parameter(Mandatory=$true)][string]$PolicyRollbackPath,
 [Parameter(Mandatory=$true)][string]$ProfileRollbackPath)
$ErrorActionPreference="Stop";Set-StrictMode -Version Latest
if(@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0){throw "Stop the Desktop Runtime Host first."}
foreach($path in @($AuthorizationPolicyPath,$ApplicationProfilePath,$PolicyRollbackPath,$ProfileRollbackPath)){if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "A restoration input is missing."}}
$policy=Get-Content $AuthorizationPolicyPath -Raw|ConvertFrom-Json;$profile=Get-Content $ApplicationProfilePath -Raw|ConvertFrom-Json
$oldPolicy=Get-Content $PolicyRollbackPath -Raw|ConvertFrom-Json;$oldProfile=Get-Content $ProfileRollbackPath -Raw|ConvertFrom-Json
if(@($policy.grants|Where-Object{$_.principalId -ceq "hase-python-automation" -and $_.permission -ceq "diagnostics.subscribe"}).Count -ne 1 -or $profile.remoteDiagnosticsEnabled -ne $true -or $profile.remoteDiagnosticsMaximumLevel -ne "Bytes"){throw "The active diagnostic state is not exact."}
$oldProfileNames=@($oldProfile.PSObject.Properties.Name)
$oldDiagnosticsEnabled=($oldProfileNames -contains "remoteDiagnosticsEnabled") -and ($oldProfile.remoteDiagnosticsEnabled -eq $true)
if(@($oldPolicy.grants|Where-Object{$_.principalId -ceq "hase-python-automation" -and $_.permission -ceq "diagnostics.subscribe"}).Count -ne 0 -or $oldDiagnosticsEnabled){throw "The rollback state is not disabled."}
$id=[Guid]::NewGuid().ToString("N");$activePolicy=$AuthorizationPolicyPath+".50i-active-"+$id;$activeProfile=$ApplicationProfilePath+".50i-active-"+$id
$policyHash=(Get-FileHash $PolicyRollbackPath -Algorithm SHA256).Hash;$profileHash=(Get-FileHash $ProfileRollbackPath -Algorithm SHA256).Hash
$policyDone=$false;$profileDone=$false
try{
 [IO.File]::Replace($PolicyRollbackPath,$AuthorizationPolicyPath,$activePolicy,$false);$policyDone=$true
 [IO.File]::Replace($ProfileRollbackPath,$ApplicationProfilePath,$activeProfile,$false);$profileDone=$true
 if((Get-FileHash $AuthorizationPolicyPath -Algorithm SHA256).Hash -ne $policyHash -or (Get-FileHash $ApplicationProfilePath -Algorithm SHA256).Hash -ne $profileHash){throw "Exact restoration validation failed."}
 Remove-Item $activePolicy,$activeProfile -Force
}catch{
 if($profileDone -and (Test-Path $activeProfile)){[IO.File]::Replace($activeProfile,$ApplicationProfilePath,$null,$false)}
 if($policyDone -and (Test-Path $activePolicy)){[IO.File]::Replace($activePolicy,$AuthorizationPolicyPath,$null,$false)}
 throw
}
Write-Host "Operation            : Restore Python diagnostics"
Write-Host "Outcome              : Succeeded"
Write-Host "Remote diagnostics   : Disabled"
Write-Host "Permission removed   : diagnostics.subscribe"
Write-Host "Exact bytes restored : True"
exit 0
