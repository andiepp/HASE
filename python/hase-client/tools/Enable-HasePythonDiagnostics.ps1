[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AuthorizationPolicyPath,
    [Parameter(Mandatory=$true)][string]$ApplicationProfilePath,
    [Parameter(Mandatory=$true)][string]$PolicyRollbackPath,
    [Parameter(Mandatory=$true)][string]$ProfileRollbackPath)
$ErrorActionPreference="Stop"; Set-StrictMode -Version Latest
if(@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0){throw "Stop the Desktop Runtime Host first."}
$paths=@($AuthorizationPolicyPath,$ApplicationProfilePath,$PolicyRollbackPath,$ProfileRollbackPath)
if($paths|Where-Object{[string]::IsNullOrWhiteSpace($_) -or -not [IO.Path]::IsPathRooted($_)}){throw "All paths must be absolute."}
if(-not(Test-Path -LiteralPath $AuthorizationPolicyPath -PathType Leaf) -or -not(Test-Path -LiteralPath $ApplicationProfilePath -PathType Leaf)){throw "An authoritative input is missing."}
if((Test-Path -LiteralPath $PolicyRollbackPath)-or(Test-Path -LiteralPath $ProfileRollbackPath)){throw "A rollback target already exists."}
$policyBytes=[IO.File]::ReadAllBytes($AuthorizationPolicyPath);$profileBytes=[IO.File]::ReadAllBytes($ApplicationProfilePath)
$policyHash=(Get-FileHash -LiteralPath $AuthorizationPolicyPath -Algorithm SHA256).Hash
$profileHash=(Get-FileHash -LiteralPath $ApplicationProfilePath -Algorithm SHA256).Hash
$policyAcl=(Get-Acl -LiteralPath $AuthorizationPolicyPath).Sddl;$profileAcl=(Get-Acl -LiteralPath $ApplicationProfilePath).Sddl
$policy=[Text.Encoding]::UTF8.GetString($policyBytes).TrimStart([char]0xFEFF)|ConvertFrom-Json
$profile=[Text.Encoding]::UTF8.GetString($profileBytes).TrimStart([char]0xFEFF)|ConvertFrom-Json
$permissions=@($policy.grants|Where-Object{$_.principalId -ceq "hase-python-automation"}|ForEach-Object{[string]$_.permission})
$expected=@("runtime-host.snapshot.read","property.authoritative.read","property.write","command.execute","observation.subscribe","property.cached.read")
if(@(Compare-Object $expected $permissions -SyncWindow 0).Count -ne 0){throw "The Python principal state is not the approved pre-50I state."}
if(@($policy.grants|Where-Object{$_.principalId -ceq "hase-python-automation" -and $_.permission -ceq "diagnostics.subscribe"}).Count -ne 0){throw "Diagnostics are already authorized."}
if([string]$profile.authorizationPolicyFilePath -ne [IO.Path]::GetFullPath($AuthorizationPolicyPath)){throw "The profile policy path is not exact."}
$profileNames=@($profile.PSObject.Properties.Name)
if($profileNames -contains "remoteDiagnosticsEnabled" -and $profile.remoteDiagnosticsEnabled -eq $true){throw "Remote diagnostics are already enabled."}
$policy.grants+=@([pscustomobject]@{principalId="hase-python-automation";permission="diagnostics.subscribe"})
if($profileNames -contains "remoteDiagnosticsEnabled"){$profile.remoteDiagnosticsEnabled=$true}else{$profile|Add-Member NoteProperty remoteDiagnosticsEnabled $true}
if($profileNames -contains "remoteDiagnosticsMaximumLevel"){$profile.remoteDiagnosticsMaximumLevel="Bytes"}else{$profile|Add-Member NoteProperty remoteDiagnosticsMaximumLevel "Bytes"}
$id=[Guid]::NewGuid().ToString("N");$policyStage=$AuthorizationPolicyPath+".50i-"+$id;$profileStage=$ApplicationProfilePath+".50i-"+$id
$policyPublished=$false;$profilePublished=$false
try{
 [IO.File]::WriteAllText($policyStage,($policy|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText($profileStage,($profile|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
 if((Get-FileHash $AuthorizationPolicyPath -Algorithm SHA256).Hash -ne $policyHash -or (Get-FileHash $ApplicationProfilePath -Algorithm SHA256).Hash -ne $profileHash){throw "An input revision changed."}
 [IO.File]::Replace($policyStage,$AuthorizationPolicyPath,$PolicyRollbackPath,$false);$policyPublished=$true
 [IO.File]::Replace($profileStage,$ApplicationProfilePath,$ProfileRollbackPath,$false);$profilePublished=$true
 if((Get-FileHash $PolicyRollbackPath -Algorithm SHA256).Hash -ne $policyHash -or (Get-FileHash $ProfileRollbackPath -Algorithm SHA256).Hash -ne $profileHash){throw "Rollback custody validation failed."}
 if((Get-Acl $AuthorizationPolicyPath).Sddl -ne $policyAcl -or (Get-Acl $PolicyRollbackPath).Sddl -ne $policyAcl -or (Get-Acl $ApplicationProfilePath).Sddl -ne $profileAcl -or (Get-Acl $ProfileRollbackPath).Sddl -ne $profileAcl){throw "ACL preservation validation failed."}
}catch{
 if($profilePublished){[IO.File]::Replace($ProfileRollbackPath,$ApplicationProfilePath,$null,$false)}
 if($policyPublished){[IO.File]::Replace($PolicyRollbackPath,$AuthorizationPolicyPath,$null,$false)}
 throw
}finally{Remove-Item $policyStage,$profileStage -Force -ErrorAction SilentlyContinue}
Write-Host "Operation            : Enable Python diagnostics"
Write-Host "Outcome              : Succeeded"
Write-Host "Permission           : diagnostics.subscribe"
Write-Host "Maximum level        : Bytes"
Write-Host "Rollback retained    : True"
Write-Host "Sensitive values     : Withheld"
exit 0
