[CmdletBinding()]
param(
    # What this installation should hold from now on. Given once, on an
    # installation that predates the installed-application record or that
    # is changing to another application; the publisher records it, and
    # every later update reads the record. Omitted, the update republishes
    # whatever the installation holds, exactly as before.
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$ApplicationProject
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-RequiredFileHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The guided HASE Client $Role is missing. Run Install-HaseClient.ps1 first."
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
        throw "The installed HASE Client $Role does not match the guided installation."
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

$publisherPath = Join-Path $PSScriptRoot "Publish-HaseClient.ps1"
if (-not (Test-Path -LiteralPath $publisherPath -PathType Leaf)) {
    throw "The lower-level HASE Client publisher was not found."
}

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\Client"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$installedApplication = Get-InstalledApplication `
    -InstallationDirectory $installationDirectory `
    -DefaultExecutableName "Hase.Client.Wpf.App.exe" `
    -DefaultProject "src\Hase.Client.Wpf.App\Hase.Client.Wpf.App.csproj"
$executableName = $installedApplication.ExecutableName
$executableFilePath = Join-Path $applicationDirectory $executableName
$runtimeHostRegistryFilePath = Join-Path $configurationDirectory "client-runtime-hosts.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Client.lnk"

# The guard follows the installed application. A fixed name would stop
# protecting anything the moment the installation holds an add-on client.
$runningClient = Get-Process `
    -Name ([System.IO.Path]::GetFileNameWithoutExtension($executableName)) `
    -ErrorAction SilentlyContinue
if ($null -ne $runningClient) {
    throw "HASE Client is running. Close it before updating the application."
}

if (-not (Test-Path -LiteralPath $executableFilePath -PathType Leaf)) {
    throw "The guided HASE Client application is missing. Run Install-HaseClient.ps1 first."
}

$registryHash = Get-RequiredFileHash `
    -Path $runtimeHostRegistryFilePath `
    -Role "Runtime Host registry"
$shortcutHash = Get-RequiredFileHash `
    -Path $shortcutPath `
    -Role "desktop shortcut"

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

$expectedArguments = '"' + $runtimeHostRegistryFilePath + '"'
if (-not [string]::Equals(
        $shortcut.Arguments,
        $expectedArguments,
        [System.StringComparison]::Ordinal)) {
    throw "The installed HASE Client shortcut arguments do not contain exactly one Runtime Host registry path."
}


# The application to publish is the one requested for this run, or else the
# one the installation holds. The checks above ran against what is installed
# now; what is installed after publication is read back from the record.
$applicationProjectToPublish = if ([string]::IsNullOrWhiteSpace($ApplicationProject)) {
    $installedApplication.Project
} else {
    $ApplicationProject
}

& $publisherPath `
    -InstallationDirectory $installationDirectory `
    -ApplicationProject $applicationProjectToPublish

# What is installed now is what the publisher recorded, which may be an
# application of a different name than the one the checks above verified.
$updatedApplication = Get-InstalledApplication `
    -InstallationDirectory $installationDirectory `
    -DefaultExecutableName $executableName `
    -DefaultProject $installedApplication.Project
$updatedExecutableName = $updatedApplication.ExecutableName
$updatedExecutableFilePath = Join-Path $applicationDirectory $updatedExecutableName

if (-not (Test-Path -LiteralPath $updatedExecutableFilePath -PathType Leaf)) {
    throw "The updated HASE Client executable was not found."
}

# A shortcut is custody and is preserved, unless the application it points
# at no longer exists under that name, in which case it is re-pointed and
# nothing else about it changes.
$shortcutRepointed = $false
if (-not [string]::Equals(
        $updatedExecutableName,
        $executableName,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $updatedExecutableFilePath
    $shortcut.IconLocation = $updatedExecutableFilePath
    $shortcut.Save()
    $repointed = $shell.CreateShortcut($shortcutPath)
    Assert-EqualPath `
        -Actual $repointed.TargetPath `
        -Expected $updatedExecutableFilePath `
        -Role "re-pointed shortcut target"
    Assert-EqualPath `
        -Actual $repointed.WorkingDirectory `
        -Expected $applicationDirectory `
        -Role "re-pointed shortcut working directory"
    if (-not [string]::Equals(
            $repointed.Arguments,
            $expectedArguments,
            [System.StringComparison]::Ordinal)) {
        throw "The re-pointed HASE Client shortcut arguments changed."
    }
    $shortcutRepointed = $true
}

if ($registryHash -ne (Get-RequiredFileHash `
        -Path $runtimeHostRegistryFilePath `
        -Role "Runtime Host registry") -or
    (-not $shortcutRepointed -and
        $shortcutHash -ne (Get-RequiredFileHash `
            -Path $shortcutPath `
            -Role "desktop shortcut"))) {
    throw "The application update changed the Runtime Host registry or shortcut custody."
}

Write-Host "HASE Client update succeeded."
Write-Host "Installation directory: $installationDirectory"
Write-Host "Application           : $(if ($updatedExecutableName -eq $executableName) { 'updated' } else { "replaced by $updatedExecutableName" })"
Write-Host "Runtime Host registry : preserved"
Write-Host "Desktop shortcut      : $(if ($shortcutRepointed) { "re-pointed to $updatedExecutableName" } else { 'preserved' })"
