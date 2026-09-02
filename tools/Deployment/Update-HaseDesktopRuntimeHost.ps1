[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-OptionalFileHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-EqualPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Actual,
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if (-not [string]::Equals(
            [System.IO.Path]::GetFullPath($Actual),
            [System.IO.Path]::GetFullPath($Expected),
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The installed Runtime Host $Role does not match the guided installation."
    }
}

function Get-InstalledApplication {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallationDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DefaultExecutableName,
        [Parameter(Mandatory = $true)]
        [string]$DefaultProject
    )

    $recordPath = Join-Path $InstallationDirectory "installed-application.json"

    # An installation published before the record existed holds the
    # application this repository ships, which is what every installation
    # predating this increment holds.
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) {
        return [pscustomobject]@{
            ExecutableName = $DefaultExecutableName
            Project = $DefaultProject
        }
    }

    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    $recordedNames = @($record.PSObject.Properties.Name)

    if (-not ($recordedNames -contains "applicationExecutable")) {
        throw "The installed-application record names no executable."
    }

    $executableName = $record.applicationExecutable
    if ([string]::IsNullOrWhiteSpace($executableName)) {
        throw "The installed-application record names no executable."
    }

    $project = $DefaultProject
    if ($recordedNames -contains "applicationProject") {
        $recordedProject = $record.applicationProject
        if (-not [string]::IsNullOrWhiteSpace($recordedProject)) {
            $project = $recordedProject
        }
    }

    return [pscustomobject]@{
        ExecutableName = $executableName
        Project = $project
    }
}

$publisherPath = Join-Path $PSScriptRoot "Publish-HaseDesktopRuntimeHost.ps1"
if (-not (Test-Path -LiteralPath $publisherPath -PathType Leaf)) {
    throw "The lower-level Desktop Runtime Host publisher was not found."
}

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$identityDirectory = Join-Path $installationDirectory "Identity"
$webView2DataDirectory = Join-Path $installationDirectory "WebView2"
$installedApplication = Get-InstalledApplication `
    -InstallationDirectory $installationDirectory `
    -DefaultExecutableName "Hase.DesktopHost.App.exe" `
    -DefaultProject "src\Hase.DesktopHost.App\Hase.DesktopHost.App.csproj"
$executableName = $installedApplication.ExecutableName
$executableFilePath = Join-Path $applicationDirectory $executableName
$legacyWebView2DataDirectory = Join-Path `
    $applicationDirectory `
    "$executableName.WebView2"
$applicationProfilePath = Join-Path $configurationDirectory "desktop-runtime-host.json"
$endpointCompositionPath = Join-Path $configurationDirectory "desktop-runtime-endpoints.json"
$privateNetworkConfigurationPath = Join-Path $configurationDirectory "desktop-private-network.json"
$identityFilePath = Join-Path $identityDirectory "runtime-host-identity.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Runtime Host.lnk"

$requiredFiles = @(
    $executableFilePath,
    $applicationProfilePath,
    $endpointCompositionPath,
    $privateNetworkConfigurationPath,
    $shortcutPath
)

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The guided Runtime Host installation is incomplete. Run Install-HaseDesktopRuntimeHost.ps1 first."
    }
}

if (-not (Test-Path -LiteralPath $identityDirectory -PathType Container)) {
    throw "The guided Runtime Host identity directory is missing. Run Install-HaseDesktopRuntimeHost.ps1 first."
}

if (Test-Path -LiteralPath $webView2DataDirectory -PathType Leaf) {
    throw "The durable Runtime Host WebView2 custody path is a file."
}

$webView2PresentBefore = Test-Path `
    -LiteralPath $webView2DataDirectory `
    -PathType Container
$legacyWebView2PresentBefore = Test-Path `
    -LiteralPath $legacyWebView2DataDirectory `
    -PathType Container
if ($webView2PresentBefore -and $legacyWebView2PresentBefore) {
    throw "Both legacy and durable Runtime Host WebView2 custody exist."
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
Assert-EqualPath `
    -Actual $shortcut.TargetPath `
    -Expected $executableFilePath `
    -Role "shortcut target"
Assert-EqualPath `
    -Actual $shortcut.WorkingDirectory `
    -Expected $applicationDirectory `
    -Role "shortcut working directory"

$expectedArguments = '"' + $applicationProfilePath + '"'
if (-not [string]::Equals(
        $shortcut.Arguments,
        $expectedArguments,
        [System.StringComparison]::Ordinal)) {
    throw "The installed Runtime Host shortcut arguments do not contain exactly one application-profile path."
}

$applicationProfileHash = Get-OptionalFileHash -Path $applicationProfilePath
$endpointCompositionHash = Get-OptionalFileHash -Path $endpointCompositionPath
$privateNetworkConfigurationHash = Get-OptionalFileHash -Path $privateNetworkConfigurationPath
$shortcutHash = Get-OptionalFileHash -Path $shortcutPath
$identityHash = Get-OptionalFileHash -Path $identityFilePath
$applicationProfile = Get-Content `
    -LiteralPath $applicationProfilePath `
    -Raw `
    -Encoding UTF8 | ConvertFrom-Json
$applicationProfilePropertyNames = @(
    $applicationProfile.PSObject.Properties.Name)
$authorizationPolicyHash = $null
$authorizationPolicyPath = $null
$mediaConfigurationHash = $null
$mediaConfigurationPath = $null
if ($applicationProfilePropertyNames -contains "authorizationPolicyFilePath") {
    if ([string]::IsNullOrWhiteSpace(
            $applicationProfile.authorizationPolicyFilePath)) {
        throw "The installed Runtime Host authorization-policy path is invalid."
    }

    $authorizationPolicyPath =
        [System.IO.Path]::GetFullPath(
            $applicationProfile.authorizationPolicyFilePath)
    $expectedAuthorizationPolicyPath = Join-Path `
        $configurationDirectory `
        "runtime-host-authorization.json"
    Assert-EqualPath `
        -Actual $authorizationPolicyPath `
        -Expected $expectedAuthorizationPolicyPath `
        -Role "authorization-policy path"
    if (-not (Test-Path `
            -LiteralPath $authorizationPolicyPath `
            -PathType Leaf)) {
        throw "The installed Runtime Host authorization policy is missing."
    }

    $authorizationPolicyHash = Get-OptionalFileHash `
        -Path $authorizationPolicyPath
}

if ($applicationProfilePropertyNames -contains "mediaConfigurationFilePath") {
    if ([string]::IsNullOrWhiteSpace(
            $applicationProfile.mediaConfigurationFilePath)) {
        throw "The installed Runtime Host media-configuration path is invalid."
    }
    $mediaConfigurationPath = [System.IO.Path]::GetFullPath(
        $applicationProfile.mediaConfigurationFilePath)
    $expectedMediaConfigurationPath = Join-Path `
        $configurationDirectory `
        "desktop-runtime-media.json"
    Assert-EqualPath -Actual $mediaConfigurationPath `
        -Expected $expectedMediaConfigurationPath `
        -Role "media-configuration path"
    if (-not (Test-Path -LiteralPath $mediaConfigurationPath -PathType Leaf)) {
        throw "The installed Runtime Host media configuration is missing."
    }
    $mediaConfigurationHash = Get-OptionalFileHash -Path $mediaConfigurationPath
}

& $publisherPath `
    -InstallationDirectory $installationDirectory `
    -ApplicationProject $installedApplication.Project

if (-not (Test-Path -LiteralPath $executableFilePath -PathType Leaf)) {
    throw "The updated Desktop Runtime Host executable was not found."
}

$webView2ExpectedAfter =
    $webView2PresentBefore -or $legacyWebView2PresentBefore
if ($webView2ExpectedAfter -and
    -not (Test-Path `
        -LiteralPath $webView2DataDirectory `
        -PathType Container)) {
    throw "The Runtime Host WebView2 custody was not preserved."
}
if (Test-Path `
    -LiteralPath $legacyWebView2DataDirectory `
    -PathType Container) {
    throw "Legacy WebView2 custody remained inside the replaceable application directory."
}

$authorizationPolicyChanged = $false
if ($null -ne $authorizationPolicyPath) {
    $authorizationPolicyChanged =
        $authorizationPolicyHash -ne (Get-OptionalFileHash -Path $authorizationPolicyPath)
}
$mediaConfigurationChanged = $false
if ($null -ne $mediaConfigurationPath) {
    $mediaConfigurationChanged = $mediaConfigurationHash -ne `
        (Get-OptionalFileHash -Path $mediaConfigurationPath)
}

if ($applicationProfileHash -ne (Get-OptionalFileHash -Path $applicationProfilePath) -or
    $endpointCompositionHash -ne (Get-OptionalFileHash -Path $endpointCompositionPath) -or
    $privateNetworkConfigurationHash -ne (Get-OptionalFileHash -Path $privateNetworkConfigurationPath) -or
    $shortcutHash -ne (Get-OptionalFileHash -Path $shortcutPath) -or
    $identityHash -ne (Get-OptionalFileHash -Path $identityFilePath) -or
    $authorizationPolicyChanged -or
    $mediaConfigurationChanged) {
    throw "The application update changed configuration, identity, or shortcut custody."
}

Write-Host "HASE Desktop Runtime Host update succeeded."
Write-Host "Installation directory : $installationDirectory"
Write-Host "Application             : updated"
Write-Host "Configuration profiles  : preserved"
Write-Host "Private-network settings: preserved"
Write-Host "Installation identity   : preserved"
Write-Host "Desktop shortcut        : preserved"
Write-Host "WebView2 custody        : $(if ($legacyWebView2PresentBefore) { 'migrated' } elseif ($webView2PresentBefore) { 'preserved' } else { 'ready for initialization' })"
