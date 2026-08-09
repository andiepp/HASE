[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MiniPcConfigurationPath,
    [Parameter(Mandatory=$true)][string]$ApplicationProfilePath,
    [Parameter(Mandatory=$true)][string]$TrustedServerCertificatePath,
    [Parameter(Mandatory=$true)][string]$AuthorityManifestPath,
    [Parameter(Mandatory=$true)][string]$AuthorityRollbackEvidencePath,
    [Parameter(Mandatory=$true)][string]$ProvisioningDirectory,
    [Parameter(Mandatory=$true)][string]$SourceProfilePath,
    [Parameter(Mandatory=$true)][string]$CertificatePath,
    [Parameter(Mandatory=$true)][string]$PrivateKeyPath,
    [Parameter(Mandatory=$true)][string]$ProfilePath,
    [Parameter(Mandatory=$true)][string]$AuthorizationPolicyPath,
    [Parameter(Mandatory=$true)][string]$RollbackDirectory,
    [Parameter(Mandatory=$true)][ValidateRange(1,90)][int]$ValidityDays
)
$ErrorActionPreference="Stop"; Set-StrictMode -Version Latest
$rollbackCreated=$false; $templateCreated=$false
function Full([string]$v){if([string]::IsNullOrWhiteSpace($v)-or $v-ne $v.Trim()-or $v-notmatch '^[A-Za-z]:[\\/]'){throw "path"};[IO.Path]::GetFullPath($v)}
function Within([string]$p,[string]$c){$x=(Full $p).TrimEnd("\")+"\";(Full $c).StartsWith($x,[StringComparison]::OrdinalIgnoreCase)}
function PrivateDirectory([string]$p){$u=[Security.Principal.WindowsIdentity]::GetCurrent().User;$a=[Security.AccessControl.DirectorySecurity]::new();$a.SetOwner($u);$a.SetAccessRuleProtection($true,$false);$a.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($u,"FullControl","ContainerInherit,ObjectInherit","None","Allow"));Set-Acl $p $a}
function PrivateFile([string]$p){$u=[Security.Principal.WindowsIdentity]::GetCurrent().User;$a=[Security.AccessControl.FileSecurity]::new();$a.SetOwner($u);$a.SetAccessRuleProtection($true,$false);$a.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($u,"FullControl","Allow"));Set-Acl $p $a}
try{
 if($env:OS-ne"Windows_NT"){throw "platform"}
 $tool=Split-Path -Parent $MyInvocation.MyCommand.Path;$pkg=Split-Path -Parent $tool;$repo=Full(Split-Path -Parent(Split-Path -Parent $pkg))
 if(@(&git -C $repo status --porcelain).Count-ne0){throw"repository"};$head=(&git -C $repo rev-parse HEAD).Trim();$origin=(&git -C $repo rev-parse origin/main).Trim();if($head-ne$origin){throw"repository"}
 if(@(Get-Process Hase.DesktopHost.App,Hase.Client.Wpf.App -ErrorAction SilentlyContinue).Count-ne0){throw"processes"}
 $config=Full $MiniPcConfigurationPath;$app=Full $ApplicationProfilePath;$trusted=Full $TrustedServerCertificatePath;$authority=Full $AuthorityManifestPath;$authorityRollback=Full $AuthorityRollbackEvidencePath
 $root=Full $ProvisioningDirectory;$template=Full $SourceProfilePath;$cert=Full $CertificatePath;$key=Full $PrivateKeyPath;$profile=Full $ProfilePath;$policy=Full $AuthorizationPolicyPath;$script:rollback=Full $RollbackDirectory
 foreach($f in @($config,$app,$trusted,$authority,$authorityRollback)){if(-not(Test-Path $f -PathType Leaf)){throw"input"}}
 foreach($p in @($root,$template,$cert,$key,$profile,$policy,$script:rollback)){if(Test-Path $p){throw"target"}}
 if(-not(Within $root $cert)-or-not(Within $root $key)-or-not(Within $root $profile)-or(Within $root $template)-or(Within $repo $template)-or(Within $repo $script:rollback)){throw"custody"}
 if(-not(Test-Path(Split-Path -Parent $root)-PathType Container)-or-not(Test-Path(Split-Path -Parent $template)-PathType Container)-or-not(Test-Path(Split-Path -Parent $policy)-PathType Container)-or-not(Test-Path(Split-Path -Parent $script:rollback)-PathType Container)){throw"parent"}
 $c=Get-Content $config -Raw|ConvertFrom-Json;$a=Get-Content $app -Raw|ConvertFrom-Json;$ap=@($a.PSObject.Properties.Name)
 if($ap-notcontains"privateNetworkConfigurationFilePath"-or(Full([string]$a.privateNetworkConfigurationFilePath))-ne$config-or$ap-contains"authorizationPolicyFilePath"){throw"profile"}
 $enrollment=Full([string]$c.clientEnrollmentFilePath);$e=Get-Content $enrollment -Raw|ConvertFrom-Json
 if(@($e.enrollments|? principalId -eq "hase-python-automation").Count-ne0){throw"identity"}
 $principals=@($e.enrollments|%{[string]$_.principalId}|Sort-Object -Unique);$trust=@($e.enrollments|%{[string]$_.trustPolicyId}|Sort-Object -Unique);if($principals.Count-lt1-or$trust.Count-ne1){throw"enrollment"}
 $m=Get-Content $authority -Raw|ConvertFrom-Json;$r=Get-Content $authorityRollback -Raw|ConvertFrom-Json;if($m.thumbprint-cne$r.thumbprint-or$m.certificateSha256-cne$r.certificateSha256){throw"authority"}
 $my=@(Get-ChildItem Cert:\CurrentUser\My|? Thumbprint -eq $m.thumbprint);$roots=@(Get-ChildItem Cert:\CurrentUser\Root|? Thumbprint -eq $m.thumbprint);if($my.Count-ne1-or$roots.Count-ne1-or-not$my[0].HasPrivateKey-or$roots[0].HasPrivateKey){throw"authority"}
 & (Join-Path $tool "Test-HasePythonCredentialProvisioningReadiness.ps1") -DesktopConfigurationPath $config -SigningRootThumbprint ([string]$m.thumbprint) *>$null;if($LASTEXITCODE-ne0){throw"readiness"}
 $server=Get-PfxCertificate $trusted;$active=@(Get-ChildItem Cert:\CurrentUser\My|? Thumbprint -eq $c.serverCertificate.thumbprint);if($active.Count-ne1-or$server.HasPrivateKey-or[Convert]::ToBase64String($server.RawData)-ne[Convert]::ToBase64String($active[0].RawData)){throw"server"}
 $ip=$null;if(-not[Net.IPAddress]::TryParse([string]$c.binding.address,[ref]$ip)){throw"binding"};$hostText=$ip.ToString();if($ip.AddressFamily-eq[Net.Sockets.AddressFamily]::InterNetworkV6){$hostText="[$hostText]"};$address="https://${hostText}:$([int]$c.binding.port)"
 [IO.Directory]::CreateDirectory($script:rollback)|Out-Null;$rollbackCreated=$true;PrivateDirectory $script:rollback;$utf8=[Text.UTF8Encoding]::new($false)
 $permissions=@("runtime-host.snapshot.read","property.cached.read","property.authoritative.read","property.write","command.execute","observation.subscribe")
 $grants=@();foreach($p in $principals){foreach($permission in $permissions){$grants+=[ordered]@{principalId=$p;permission=$permission}}}
 $initialPolicy=[ordered]@{formatVersion=1;grants=$grants};$candidateApp=[ordered]@{};foreach($prop in $a.PSObject.Properties){$candidateApp[$prop.Name]=$prop.Value};$candidateApp.authorizationPolicyFilePath=$policy
 $source=[ordered]@{formatVersion=1;address=$address;clientCertificate=[ordered]@{certificateChainPath=$cert;privateKeyPath=$key};trustedServerCertificate=[ordered]@{certificatePath=$trusted}}
 [IO.File]::WriteAllText($template,($source|ConvertTo-Json -Depth 8),$utf8);$templateCreated=$true;PrivateFile $template
 Copy-Item $enrollment (Join-Path $script:rollback "enrollment.original");Copy-Item $app (Join-Path $script:rollback "application-profile.original")
 [IO.File]::WriteAllText((Join-Path $script:rollback "authorization-policy.candidate.json"),($initialPolicy|ConvertTo-Json -Depth 8),$utf8)
 [IO.File]::WriteAllText((Join-Path $script:rollback "application-profile.candidate.json"),($candidateApp|ConvertTo-Json -Depth 8),$utf8)
 $entries=@([ordered]@{name="provisioningDirectory";path=$root;existed=$false},[ordered]@{name="certificate";path=$cert;existed=$false},[ordered]@{name="privateKey";path=$key;existed=$false},[ordered]@{name="pythonProfile";path=$profile;existed=$false},[ordered]@{name="enrollment";path=$enrollment;existed=$true;sha256=(Get-FileHash $enrollment -Algorithm SHA256).Hash.ToLowerInvariant()},[ordered]@{name="authorizationPolicy";path=$policy;existed=$false},[ordered]@{name="applicationProfile";path=$app;existed=$true;sha256=(Get-FileHash $app -Algorithm SHA256).Hash.ToLowerInvariant()})
 $plan=[ordered]@{schemaVersion=1;purpose="hase-minipc-python-provisioning-transaction";repositoryHead=$head;signingRootThumbprint=[string]$m.thumbprint;trustPolicyId=$trust[0];sourceProfilePath=$template;validityDays=$ValidityDays;pythonGrants=@("runtime-host.snapshot.read","property.authoritative.read");preservedPrincipalCount=$principals.Count;entries=$entries}
 [IO.File]::WriteAllText((Join-Path $script:rollback "transaction-plan.json"),($plan|ConvertTo-Json -Depth 10),$utf8)
 foreach($f in Get-ChildItem $script:rollback -File){PrivateFile $f.FullName}
 Write-Host "Repository baseline ready       : True";Write-Host "Runtime processes stopped       : True";Write-Host "Dedicated authority ready       : True";Write-Host "Existing Client access preserved: True";Write-Host "Minimal Python grants prepared  : True";Write-Host "Profile template prepared       : True";Write-Host "Six-file transaction prepared   : True";Write-Host "Rollback evidence secured       : True";Write-Host "Publication state unchanged     : True";Write-Host "MiniPC transaction ready        : True"
}catch{if($templateCreated-and(Test-Path $template)){Remove-Item $template -Force};if($rollbackCreated-and(Test-Path $script:rollback)){Remove-Item $script:rollback -Recurse -Force};Write-Error "MiniPC Python provisioning transaction preparation failed.";exit 1}
