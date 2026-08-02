[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HandoffPath,

    [string]$RegistryPath,

    [string]$MiniPcPrivateNetworkConfigurationPath,

    [string]$ProfileId = "minipc-runtime-host",

    [string]$DisplayName = "MiniPC Runtime Host"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "The MiniPC Client profile workflow requires Windows."
}
if (@(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0) {
    throw "The HASE Client must be closed before changing its registry."
}

$clientConfigurationDirectory = Join-Path $env:LOCALAPPDATA "HASE\Client\Configuration"
if ([string]::IsNullOrWhiteSpace($RegistryPath)) {
    $RegistryPath = Join-Path $clientConfigurationDirectory "client-runtime-hosts.json"
}
if ([string]::IsNullOrWhiteSpace($MiniPcPrivateNetworkConfigurationPath)) {
    $MiniPcPrivateNetworkConfigurationPath = Join-Path `
        $clientConfigurationDirectory `
        "minipc-private-network.json"
}

$handoff = [System.IO.Path]::GetFullPath($HandoffPath)
$registry = [System.IO.Path]::GetFullPath($RegistryPath)
$miniPcConfiguration = [System.IO.Path]::GetFullPath(
    $MiniPcPrivateNetworkConfigurationPath)

foreach ($requiredFile in @($handoff, $registry, $miniPcConfiguration)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "A required MiniPC Client profile source file does not exist."
    }
}

$registryBefore = Get-Content -LiteralPath $registry -Raw | ConvertFrom-Json
$profilesBefore = @($registryBefore.hosts)
if ($registryBefore.formatVersion -ne 1 -or
    $profilesBefore.Count -ne 1 -or
    -not [bool]$profilesBefore[0].enabled) {
    throw "The existing Client registry must contain exactly one enabled profile."
}

$desktopConfiguration = [System.IO.Path]::GetFullPath(
    [string]$profilesBefore[0].privateNetworkConfigurationFilePath)
if (-not (Test-Path -LiteralPath $desktopConfiguration -PathType Leaf)) {
    throw "The existing Desktop Runtime Host Client configuration was not found."
}

$handoffDocument = Get-Content -LiteralPath $handoff -Raw | ConvertFrom-Json
if ($handoffDocument.formatVersion -ne 1 -or
    [string]::IsNullOrWhiteSpace([string]$handoffDocument.runtimeHostId)) {
    throw "The MiniPC Runtime Host handoff failed local prevalidation."
}

$protectedHashesBefore = @(
    @($handoff, $desktopConfiguration, $miniPcConfiguration) |
        ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
)
$registryDirectory = Split-Path -Parent $registry
$registryFileName = [System.IO.Path]::GetFileName($registry)
$backupsBefore = @(
    Get-ChildItem -LiteralPath $registryDirectory -File |
        Where-Object { $_.Name -like "$registryFileName.*.backup" } |
        ForEach-Object FullName
)

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$registryToolProject = Join-Path `
    $repositoryRoot `
    "src\Hase.Client.RegistryTool\Hase.Client.RegistryTool.csproj"
$registryChanged = $false
$newBackup = $null

try {
    $null = & dotnet run `
        --project $registryToolProject `
        -c Release `
        --no-build `
        -- `
        add-enabled-from-handoff `
        $registry `
        $ProfileId `
        $DisplayName `
        $handoff `
        $miniPcConfiguration *>&1
    if ($LASTEXITCODE -ne 0) {
        throw "The strict MiniPC Client profile import failed."
    }
    $registryChanged = $true

    $backupsAfter = @(
        Get-ChildItem -LiteralPath $registryDirectory -File |
            Where-Object { $_.Name -like "$registryFileName.*.backup" } |
            ForEach-Object FullName
    )
    $newBackups = @(
        $backupsAfter | Where-Object { $_ -notin $backupsBefore }
    )
    if ($newBackups.Count -ne 1) {
        throw "The MiniPC Client profile import did not retain exactly one new registry backup."
    }
    $newBackup = $newBackups[0]

    $registryAfter = Get-Content -LiteralPath $registry -Raw | ConvertFrom-Json
    $profilesAfter = @($registryAfter.hosts)
    $desktopProfileAfter = $profilesAfter[0]
    $miniPcProfiles = @(
        $profilesAfter | Where-Object { $_.profileId -eq $ProfileId }
    )
    $distinctIdentities = @(
        $profilesAfter.expectedRuntimeHostId | Select-Object -Unique
    )
    $protectedHashesAfter = @(
        @($handoff, $desktopConfiguration, $miniPcConfiguration) |
            ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    )

    if ($registryAfter.formatVersion -ne 1 -or
        $profilesAfter.Count -ne 2 -or
        @($profilesAfter | Where-Object { [bool]$_.enabled }).Count -ne 2 -or
        $miniPcProfiles.Count -ne 1 -or
        -not [bool]$miniPcProfiles[0].enabled -or
        $miniPcProfiles[0].displayName -ne $DisplayName -or
        $miniPcProfiles[0].expectedRuntimeHostId -ne $handoffDocument.runtimeHostId -or
        [System.IO.Path]::GetFullPath(
            [string]$miniPcProfiles[0].privateNetworkConfigurationFilePath) -ne
            $miniPcConfiguration -or
        $desktopProfileAfter.profileId -ne $profilesBefore[0].profileId -or
        $desktopProfileAfter.displayName -ne $profilesBefore[0].displayName -or
        $desktopProfileAfter.expectedRuntimeHostId -ne
            $profilesBefore[0].expectedRuntimeHostId -or
        [bool]$desktopProfileAfter.enabled -ne [bool]$profilesBefore[0].enabled -or
        [System.IO.Path]::GetFullPath(
            [string]$desktopProfileAfter.privateNetworkConfigurationFilePath) -ne
            $desktopConfiguration -or
        $distinctIdentities.Count -ne 2 -or
        @(Compare-Object $protectedHashesBefore $protectedHashesAfter).Count -ne 0) {
        throw "The MiniPC Client profile postconditions failed."
    }

    Write-Host
    Write-Host "HASE MiniPC Client profile installation succeeded."
    Write-Host "Handoff validation             : Ready"
    Write-Host "Desktop Runtime Host profile   : Preserved"
    Write-Host "MiniPC Runtime Host profile    : Enabled"
    Write-Host "Authoritative host identities  : Distinct"
    Write-Host "Private Client configurations  : Preserved"
    Write-Host "Previous Client registry backup: Retained"
    Write-Host "Sensitive deployment values    : Withheld"
}
catch {
    if ($registryChanged -and $null -eq $newBackup) {
        $candidateBackups = @(
            Get-ChildItem -LiteralPath $registryDirectory -File |
                Where-Object {
                    $_.Name -like "$registryFileName.*.backup" -and
                    $_.FullName -notin $backupsBefore
                }
        )
        if ($candidateBackups.Count -eq 1) {
            $newBackup = $candidateBackups[0].FullName
        }
    }
    if ($registryChanged -and
        $null -ne $newBackup -and
        (Test-Path -LiteralPath $newBackup -PathType Leaf)) {
        Copy-Item -LiteralPath $newBackup -Destination $registry -Force
    }
    throw
}
