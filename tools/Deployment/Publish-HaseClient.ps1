[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallationDirectory,

    # The application project to publish. This tool publishes the application
    # this repository ships; a composition root that ships instruments names
    # its own project here. The project must live inside this repository.
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$ApplicationProject
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

function Get-OutermostRepositoryRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    # This repository may be consumed as a submodule of another. An add-on
    # repository that does so publishes its own applications with this
    # tooling, and those projects live in the containing repository rather
    # than in this one. The boundary this tool will publish from is therefore
    # the outermost repository that contains this one. When there is none, or
    # Git cannot say, the boundary is this repository, exactly as before.
    $superproject = & git -C $RepositoryRoot rev-parse --show-superproject-working-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($superproject)) {
        return $RepositoryRoot
    }

    return Get-NormalizedDirectory `
        -Path ([System.IO.Path]::GetFullPath($superproject.Trim())) `
        -Role "containing repository root"
}

function Resolve-ApplicationProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string]$OutermostRoot,
        [Parameter(Mandatory = $true)]
        [string]$DefaultProjectFile,
        [Parameter(Mandatory = $false)]
        [string]$RequestedProject
    )

    if ([string]::IsNullOrWhiteSpace($RequestedProject)) {
        return $DefaultProjectFile
    }

    $candidate = $RequestedProject
    if (-not [System.IO.Path]::IsPathRooted($candidate)) {
        # A relative project is looked for in this repository first and in
        # the containing repository second, so the shipped default and an
        # add-on's own project both resolve by the same rule.
        $inRepository = Join-Path $RepositoryRoot $candidate
        $candidate = if (Test-Path -LiteralPath $inRepository -PathType Leaf) {
            $inRepository
        } else {
            Join-Path $OutermostRoot $candidate
        }
    }

    $resolved = [System.IO.Path]::GetFullPath($candidate)

    if (-not (Test-SameOrChildDirectory `
            -Candidate $resolved `
            -Parent $OutermostRoot)) {
        throw "The application project must be inside this repository or the repository that contains it."
    }

    if ([System.IO.Path]::GetExtension($resolved) -ne ".csproj") {
        throw "The application project must be a .csproj file."
    }

    return $resolved
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

$defaultProjectFile = Join-Path $repositoryRoot "src\Hase.Client.Wpf.App\Hase.Client.Wpf.App.csproj"
$outermostRoot = Get-OutermostRepositoryRoot -RepositoryRoot $repositoryRoot
$projectFile = Resolve-ApplicationProject `
    -RepositoryRoot $repositoryRoot `
    -OutermostRoot $outermostRoot `
    -DefaultProjectFile $defaultProjectFile `
    -RequestedProject $ApplicationProject
if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "The HASE Client WPF application project was not found."
}

# The published executable takes its name from the project, so an add-on
# application publishes under its own name rather than the base one.
$applicationName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile)
$executableName = "$applicationName.exe"

# Recorded relative to the root it was found under, and which root that
# was, so an update republishes the application the installation actually
# holds rather than the shipped one, whether it is this repository's own
# application or an add-on's.
$applicationProjectRoot = if (Test-SameOrChildDirectory `
        -Candidate $projectFile `
        -Parent $repositoryRoot) {
    "repository"
} else {
    "superproject"
}
$applicationProjectRootPath = if ($applicationProjectRoot -eq "repository") {
    $repositoryRoot
} else {
    $outermostRoot
}
$applicationProjectRelative = $projectFile.Substring($applicationProjectRootPath.Length).TrimStart(
    [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar))

$applicationDirectory = Join-Path $installationRoot "Application"
$configurationDirectory = Join-Path $installationRoot "Configuration"
$executableFile = Join-Path $applicationDirectory $executableName
$installedApplicationFile = Join-Path $installationRoot "installed-application.json"
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

    $stagedExecutable = Join-Path $stagingDirectory $executableName
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

    # Recorded only once the application is installed and verified, so the
    # record always describes the application actually present. A failure
    # restores the previous application and leaves the previous record.
    $installedApplication = [ordered]@{
        formatVersion = 1
        applicationExecutable = $executableName
        applicationProject = $applicationProjectRelative
        applicationProjectRoot = $applicationProjectRoot
    }
    $installedApplication |
        ConvertTo-Json |
        Set-Content -LiteralPath $installedApplicationFile -Encoding utf8

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
