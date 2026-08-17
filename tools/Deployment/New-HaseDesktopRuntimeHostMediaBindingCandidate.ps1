[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [string]$RepositoryPath = "H:\Development",
    [string]$MediaSourceId = "camera-01",
    [string]$DisplayName = "Runtime Host Camera",
    [string]$OutputPath = $(Join-Path $env:LOCALAPPDATA `
        "HASE\RuntimeHost\Preparation\desktop-runtime-media.candidate.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

if ($env:COMPUTERNAME -cne "AEPRAKETE") {
    throw "Run this tool only on AEPRAKETE."
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
Assert-HaseApplicationsStopped

if ($MediaSourceId -notmatch '^[a-z0-9][a-z0-9.-]{0,63}$') {
    throw "The media source identity must use one to sixty-four lowercase safe characters."
}
if ([string]::IsNullOrWhiteSpace($DisplayName) -or
    $DisplayName.Length -gt 80 -or $DisplayName.Contains('"')) {
    throw "The media display name is invalid."
}
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not [System.IO.Path]::IsPathRooted($OutputPath) -or
    $OutputPath -match '^[A-Za-z]:[^\\/]') {
    throw "The media binding candidate output path must be fully qualified."
}
if (Test-Path -LiteralPath $outputFullPath) {
    throw "The media binding candidate output already exists."
}

$applicationRoot = Join-Path $env:LOCALAPPDATA `
    "HASE\RuntimeHost\Application"
$executablePath = Join-Path $applicationRoot "Hase.DesktopHost.App.exe"
foreach ($required in @(
    $executablePath,
    (Join-Path $applicationRoot "Microsoft.Web.WebView2.Core.dll"),
    (Join-Path $applicationRoot "Microsoft.Web.WebView2.Wpf.dll"),
    (Join-Path $applicationRoot "Media\Assets\binding.html"),
    (Join-Path $applicationRoot "Media\Assets\binding.js"),
    (Join-Path $applicationRoot "Media\Assets\binding.css"))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "The updated Runtime Host binding application is incomplete."
    }
}

$outputDirectory = Split-Path -Parent $outputFullPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $outputDirectory)
}
$directoryAcl = Get-Acl -LiteralPath $outputDirectory
$directoryAcl.SetAccessRuleProtection($true, $false)
$directoryAcl.SetAccessRule(
    (New-Object System.Security.AccessControl.FileSystemAccessRule(
        [System.Security.Principal.WindowsIdentity]::GetCurrent().Name,
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow")))
$directoryAcl.SetAccessRule(
    (New-Object System.Security.AccessControl.FileSystemAccessRule(
        "SYSTEM",
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow")))
Set-Acl -LiteralPath $outputDirectory -AclObject $directoryAcl

$generation = [System.Guid]::NewGuid().ToString("N")
$processArguments = @(
    "--prepare-media-binding",
    ('"' + $outputFullPath + '"'),
    $MediaSourceId,
    $generation,
    ('"' + $DisplayName + '"')
)
$process = Start-Process -FilePath $executablePath `
    -ArgumentList $processArguments -Wait -PassThru
if ($process.ExitCode -ne 0) {
    throw "The local Runtime Host media binding process failed."
}

$candidate = Read-HaseBoundedJson $outputFullPath `
    "media binding candidate"
Assert-HaseExactProperties $candidate @("formatVersion", "sources") `
    "media binding candidate"
$sources = @($candidate.sources)
if ([int]$candidate.formatVersion -ne 1 -or $sources.Count -ne 1 -or
    [string]::IsNullOrWhiteSpace([string]$sources[0].videoDeviceId)) {
    throw "The media binding candidate is not valid."
}
$candidateHash = Get-HaseRequiredFileHash $outputFullPath `
    "media binding candidate"
$audioConfigured = -not [string]::IsNullOrWhiteSpace(
    [string]$sources[0].audioDeviceId)

Write-Host ""
Write-Host "ADR-0055 Runtime Host media binding candidate prepared"
Write-Host ""
Write-Host "Computer exact             :" ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Repository commit exact    :" $true
Write-Host "Candidate path             :" $outputFullPath
Write-Host "Candidate SHA-256          :" $candidateHash
Write-Host "Camera selected            :" $true
Write-Host "Microphone selected        :" $audioConfigured
Write-Host "Device identifiers withheld:" $true
Write-Host "Candidate active           :" $false
Write-Host ""
Write-Host "Temporary local media access was explicitly operator initiated and"
Write-Host "released. No Runtime Host service, signaling, deployment, configuration,"
Write-Host "authorization, credential, serial, firmware, or physical output changed."
