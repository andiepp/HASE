[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallationDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-NormalizedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "The $Role must not be empty or whitespace."
    }

    if (-not [System.IO.Path]::IsPathRooted($Path) -or
        $Path -match '^[A-Za-z]:[^\\/]') {
        throw "The $Role must be fully qualified."
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $pathRoot = [System.IO.Path]::GetPathRoot($fullPath)

    if ([string]::Equals(
            $fullPath,
            $pathRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        return $pathRoot
    }

    return $fullPath.TrimEnd([char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar))
}

function Test-SameOrChildDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Candidate,
        [Parameter(Mandatory = $true)]
        [string]$Parent
    )

    $comparison = [System.StringComparison]::OrdinalIgnoreCase
    if ([string]::Equals($Candidate, $Parent, $comparison)) {
        return $true
    }

    $separator = [System.IO.Path]::DirectorySeparatorChar
    return $Candidate.StartsWith($Parent + $separator, $comparison)
}

$repositoryRoot = Get-NormalizedDirectory `
    -Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) `
    -Role "repository root"
$installationRoot = Get-NormalizedDirectory `
    -Path $InstallationDirectory `
    -Role "installation directory"
$filesystemRoot = [System.IO.Path]::GetPathRoot($installationRoot)

if ([string]::Equals(
        $installationRoot,
        $filesystemRoot,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The installation directory must not be a filesystem root."
}

if (Test-SameOrChildDirectory -Candidate $installationRoot -Parent $repositoryRoot) {
    throw "The installation directory must not be the repository or a directory inside it."
}

$projectFile = Join-Path $repositoryRoot "src\Hase.Client.Wpf.App\Hase.Client.Wpf.App.csproj"
if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "The HASE Client WPF application project was not found."
}

$applicationDirectory = Join-Path $installationRoot "Application"
$configurationDirectory = Join-Path $installationRoot "Configuration"
$executableFile = Join-Path $applicationDirectory "Hase.Client.Wpf.App.exe"
$installationWasUpdate = Test-Path -LiteralPath $applicationDirectory -PathType Container
$operationId = [Guid]::NewGuid().ToString("N")
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "hase-client-publish-$operationId"
$backupDirectory = Join-Path $installationRoot ".Application.previous-$operationId"
$applicationMovedToBackup = $false
$stagingInstalled = $false

try {
    New-Item -ItemType Directory -Path $stagingDirectory | Out-Null

    & dotnet publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $stagingDirectory `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "HASE Client Release publication failed with exit code $LASTEXITCODE."
    }

    $stagedExecutable = Join-Path $stagingDirectory "Hase.Client.Wpf.App.exe"
    if (-not (Test-Path -LiteralPath $stagedExecutable -PathType Leaf)) {
        throw "Publication completed without the expected HASE Client executable."
    }

    New-Item -ItemType Directory -Path $installationRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $configurationDirectory -Force | Out-Null

    if (Test-Path -LiteralPath $applicationDirectory) {
        Move-Item -LiteralPath $applicationDirectory -Destination $backupDirectory
        $applicationMovedToBackup = $true
    }

    Move-Item -LiteralPath $stagingDirectory -Destination $applicationDirectory
    $stagingInstalled = $true

    if (-not (Test-Path -LiteralPath $executableFile -PathType Leaf)) {
        throw "The installed HASE Client executable could not be verified."
    }

    if ($applicationMovedToBackup) {
        Remove-Item -LiteralPath $backupDirectory -Recurse -Force
        $applicationMovedToBackup = $false
    }
}
catch {
    if ($stagingInstalled -and (Test-Path -LiteralPath $applicationDirectory)) {
        Remove-Item -LiteralPath $applicationDirectory -Recurse -Force
    }

    if ($applicationMovedToBackup -and (Test-Path -LiteralPath $backupDirectory)) {
        Move-Item -LiteralPath $backupDirectory -Destination $applicationDirectory
        $applicationMovedToBackup = $false
    }

    throw
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

$operation = if ($installationWasUpdate) { "Application update" } else { "New installation" }

Write-Host "HASE Client publication succeeded."
Write-Host "Operation             : $operation"
Write-Host "Configuration         : Release"
Write-Host "Runtime identifier    : win-x64"
Write-Host "Self-contained        : true"
Write-Host "Installation directory: $installationRoot"
Write-Host "Executable             : $executableFile"
Write-Host "Configuration custody : $configurationDirectory"
