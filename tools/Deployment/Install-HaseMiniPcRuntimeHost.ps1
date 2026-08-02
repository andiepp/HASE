[CmdletBinding()]
param(
    [string]$PrivateNetworkConfigurationPath = (
        [System.IO.Path]::Combine(
            $env:LOCALAPPDATA,
            "HASE",
            "SecondRuntimeHostProvisioning",
            "RuntimeHostSecurity",
            "desktop-private-network.json")
    )
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "The MiniPC Runtime Host installation requires Windows."
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$privateNetworkPath = [System.IO.Path]::GetFullPath(
    $PrivateNetworkConfigurationPath)
$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$identityPath = Join-Path `
    $installationDirectory `
    "Identity\runtime-host-identity.json"
$shortcutPath = Join-Path `
    ([Environment]::GetFolderPath("Desktop")) `
    "HASE Runtime Host.lnk"

if (Test-Path -LiteralPath $installationDirectory) {
    throw "MiniPC installation refused because a Runtime Host installation already exists."
}
if (Test-Path -LiteralPath $shortcutPath) {
    throw "MiniPC installation refused because a Runtime Host shortcut already exists."
}
if (-not (Test-Path -LiteralPath $privateNetworkPath -PathType Leaf)) {
    throw "The provisioned MiniPC private-network configuration was not found."
}

$privateNetwork = Get-Content `
    -LiteralPath $privateNetworkPath `
    -Raw |
    ConvertFrom-Json
$enrollmentPath = [System.IO.Path]::GetFullPath(
    [string]$privateNetwork.clientEnrollmentFilePath)
if (-not (Test-Path -LiteralPath $enrollmentPath -PathType Leaf)) {
    throw "The provisioned MiniPC client enrollment was not found."
}

$securityHashesBefore = @(
    @($privateNetworkPath, $enrollmentPath) |
        ForEach-Object {
            (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        }
)
$certificateThumbprint = [string]$privateNetwork.serverCertificate.thumbprint
$certificateCountBefore = @(
    Get-ChildItem "Cert:\CurrentUser\My" |
        Where-Object { $_.Thumbprint -eq $certificateThumbprint }
).Count

$preflightPath = Join-Path `
    $PSScriptRoot `
    "Test-HaseSecondPcRuntimeHostPreflight.ps1"
$installerPath = Join-Path `
    $PSScriptRoot `
    "Install-HaseDesktopRuntimeHost.ps1"
$protocolExplorerProject = Join-Path `
    $repositoryRoot `
    "src\HASE.ProtocolExplorer\HASE.ProtocolExplorer.csproj"
$auditProject = Join-Path `
    $repositoryRoot `
    "src\Hase.DesktopHost.OnboardingAudit\Hase.DesktopHost.OnboardingAudit.csproj"

$installationAttempted = $false
try {
    $null = & $preflightPath `
        -PrivateNetworkConfigurationPath $privateNetworkPath `
        -CompactVendorId "0x2341" `
        -CompactProductId "0x0001" *>&1

    $arduinoCandidates = @(
        Get-CimInstance -ClassName Win32_PnPEntity |
            Where-Object {
                $_.PNPDeviceID -match 'VID_2341&PID_0001' -and
                $_.Name -match '\(COM[0-9]+\)'
            }
    )
    if ($arduinoCandidates.Count -ne 1) {
        throw "Exactly one MiniPC Arduino USB candidate is required."
    }
    $match = [regex]::Match(
        [string]$arduinoCandidates[0].Name,
        '\((COM[0-9]+)\)')
    if (-not $match.Success) {
        throw "The MiniPC Arduino COM port could not be resolved."
    }
    $portName = $match.Groups[1].Value

    $null = & dotnet run `
        --project $protocolExplorerProject `
        -c Release `
        --no-build `
        -- `
        c020 `
        $portName *>&1
    if ($LASTEXITCODE -ne 0) {
        throw "The MiniPC Arduino failed authoritative Compact validation."
    }

    $installationAttempted = $true
    & $installerPath `
        -EndpointCompositionMode "CompactSerialOnly" `
        -CompactExpectedEndpointId "arduino-uno-01" `
        -CompactVendorId "0x2341" `
        -CompactProductId "0x0001" `
        -CompactBaudRate 115200 `
        -CompactVerificationTimeoutMilliseconds 3000 `
        -PrivateNetworkConfigurationPath $privateNetworkPath

    $null = & dotnet run `
        --project $auditProject `
        -c Release `
        --no-build `
        -- `
        create-identity `
        $identityPath *>&1
    if ($LASTEXITCODE -ne 0) {
        throw "The MiniPC Runtime Host identity could not be created."
    }

    $null = & dotnet run `
        --project $auditProject `
        -c Release `
        --no-build `
        -- `
        $installationDirectory *>&1
    if ($LASTEXITCODE -ne 0) {
        throw "The MiniPC Runtime Host installation audit failed."
    }

    $securityHashesAfter = @(
        @($privateNetworkPath, $enrollmentPath) |
            ForEach-Object {
                (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
            }
    )
    $certificateCountAfter = @(
        Get-ChildItem "Cert:\CurrentUser\My" |
            Where-Object { $_.Thumbprint -eq $certificateThumbprint }
    ).Count

    if (@(Compare-Object $securityHashesBefore $securityHashesAfter).Count -ne 0 -or
        $certificateCountBefore -ne $certificateCountAfter) {
        throw "Provisioned MiniPC security changed during installation."
    }

    Write-Host
    Write-Host "HASE Arduino-only MiniPC Runtime Host installation succeeded."
    Write-Host "Security preflight        : Ready"
    Write-Host "Authoritative Arduino     : Ready"
    Write-Host "Endpoint composition      : CompactSerialOnly"
    Write-Host "Runtime Host identity     : Created"
    Write-Host "Installation audit        : Ready"
    Write-Host "Provisioned security      : Preserved"
    Write-Host "Sensitive deployment values: Withheld"
}
catch {
    if ($installationAttempted) {
        if (Test-Path -LiteralPath $shortcutPath) {
            Remove-Item -LiteralPath $shortcutPath -Force
        }
        if (Test-Path -LiteralPath $installationDirectory) {
            Remove-Item -LiteralPath $installationDirectory -Recurse -Force
        }
    }
    throw
}
