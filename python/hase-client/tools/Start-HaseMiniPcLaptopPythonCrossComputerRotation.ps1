[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RotationRequestPath,
    [Parameter(Mandatory=$true)][string]$ProfileTemplatePath,
    [Parameter(Mandatory=$true)][string]$EnrollmentPath,
    [Parameter(Mandatory=$true)][string]$AuthorizationPolicyPath,
    [Parameter(Mandatory=$true)][string]$AuthorityManifestPath,
    [Parameter(Mandatory=$true)][string]$RuntimeConfigurationPath,
    [Parameter(Mandatory=$true)][string]$ProvisioningDirectory,
    [Parameter(Mandatory=$true)][string]$TransferArchivePath,
    [Parameter(Mandatory=$true)][ValidateRange(1,90)][int]$ValidityDays,
    [Parameter(Mandatory=$true)][string]$ExpectedComputer)
$ErrorActionPreference="Stop";Set-StrictMode -Version Latest
function A([string]$v){if([string]::IsNullOrWhiteSpace($v)-or $v-ne$v.Trim()-or $v-notmatch'^[A-Za-z]:[\\/]'){throw"path"};[IO.Path]::GetFullPath($v)}
function PrivateDir([string]$p){$u=[Security.Principal.WindowsIdentity]::GetCurrent().User;$a=[Security.AccessControl.DirectorySecurity]::new();$a.SetOwner($u);$a.SetAccessRuleProtection($true,$false);$a.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($u,"FullControl","ContainerInherit,ObjectInherit","None","Allow"));Set-Acl $p $a}
try{
 if($env:OS-ne"Windows_NT"-or$env:COMPUTERNAME-cne$ExpectedComputer){throw"machine"}
 $td=Split-Path -Parent $MyInvocation.MyCommand.Path;$root=[IO.Path]::GetFullPath((Join-Path $td "..\..\.."))
 if(@(& git -C $root status --porcelain).Count-ne0-or(& git -C $root rev-parse HEAD).Trim()-ne(& git -C $root rev-parse origin/main).Trim()){throw"repository"}
 if(@(Get-Process Hase.DesktopHost.App -ErrorAction SilentlyContinue).Count-ne0-or@(Get-Process Hase.Client.Wpf.App -ErrorAction SilentlyContinue).Count-ne0){throw"processes"}
 $rp=A $RotationRequestPath;$pt=A $ProfileTemplatePath;$en=A $EnrollmentPath;$po=A $AuthorizationPolicyPath;$am=A $AuthorityManifestPath;$rc=A $RuntimeConfigurationPath;$pd=A $ProvisioningDirectory;$ta=A $TransferArchivePath
 foreach($i in @($rp,$pt,$en,$po,$am,$rc)){if(-not(Test-Path $i -PathType Leaf)){throw"input"}}
 if(Test-Path $pd){throw"output"};if(-not[string]::Equals((Split-Path -Parent $ta),$pd,[StringComparison]::OrdinalIgnoreCase)){throw"custody"}
 $r=Get-Content $rp -Raw|ConvertFrom-Json;$m=Get-Content $am -Raw|ConvertFrom-Json;$e=Get-Content $en -Raw|ConvertFrom-Json
 $c=Get-Content $rc -Raw|ConvertFrom-Json;$server=@(Get-ChildItem Cert:\CurrentUser\My|?{$_.Thumbprint-ieq[string]$c.serverCertificate.thumbprint})
 if($server.Count-ne1){throw"server"};$sha=[Security.Cryptography.SHA256]::Create();try{$raw=$sha.ComputeHash($server[0].RawData);try{$sh=[BitConverter]::ToString($raw).Replace("-","").ToLowerInvariant()}finally{[Array]::Clear($raw,0,$raw.Length)}}finally{$sha.Dispose()}
 if($sh-cne[string]$r.trustedServerCertificateSha256){throw"server"}
 $mch=@($e.enrollments|?{[string]$_.credentialId-ceq[string]$r.expectedCurrentCredentialId-and[string]$_.principalId-ceq"hase-laptop-python-minipc"})
 $head=(& git -C $root rev-parse HEAD).Trim();$ph=(Get-FileHash $pt -Algorithm SHA256).Hash.ToLowerInvariant()
 if($mch.Count-ne1-or[string]$r.repositoryHead-cne$head-or[string]$r.profileSha256-cne$ph){throw"revision"}
 $trust=[string]$mch[0].trustPolicyId;if([string]::IsNullOrWhiteSpace($trust)){throw"trust"}
 [IO.Directory]::CreateDirectory($pd)|Out-Null;PrivateDir $pd
 $proj=Join-Path $root "src\Hase.Python.CredentialProvisioning.Operator\Hase.Python.CredentialProvisioning.Operator.csproj"
 $eh=(Get-FileHash $en -Algorithm SHA256).Hash.ToLowerInvariant();$oh=(Get-FileHash $po -Algorithm SHA256).Hash.ToLowerInvariant()
 $arguments=@("run","--project",$proj,"--configuration","Release","--no-build","--","rotate-cross-computer-begin","--rotation-request",$rp,"--profile-template",$pt,"--enrollment",$en,"--authorization-policy",$po,"--provisioning-directory",$pd,"--transfer-archive",$ta,"--signing-root-thumbprint",[string]$m.thumbprint,"--trust-policy-id",$trust,"--validity-days",[string]$ValidityDays,"--expected-enrollment-sha256",$eh,"--expected-authorization-policy-sha256",$oh)
 & dotnet @arguments;if($LASTEXITCODE-ne0){throw"operator"}
 & (Join-Path $td "Test-HaseMiniPcLaptopPythonCrossComputerRotation.ps1") -RotationRequestPath $rp -EnrollmentPath $en -AuthorizationPolicyPath $po -ProvisioningDirectory $pd -TransferArchivePath $ta
 if($LASTEXITCODE-ne0){throw"validation"}
}catch{Write-Error "MiniPC Laptop Python cross-computer rotation Begin failed.";exit 1}
