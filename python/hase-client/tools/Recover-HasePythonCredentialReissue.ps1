<#
.SYNOPSIS
Recovers or completes a Python credential re-issue (ADR-0083).

.DESCRIPTION
Reads the re-issue journal beside the Python profile and runs the
credential-provisioning operator's reissue-recover with the paths and the
original hashes the journal records. An interrupted re-issue is rolled
back to the exact expired sources; a committed one has its retained old
files removed, which is the explicit cleanup after the replacement was
proved. The Runtime Host and the Client must be stopped.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ExpectedComputer,
    [string] $PythonProfilePath = (Join-Path $env:LOCALAPPDATA `
        "HASE\PythonAutomation\Security\runtime-host-profile.json")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

try
{
    if ($env:OS -ne "Windows_NT" -or $env:COMPUTERNAME -cne $ExpectedComputer)
    {
        throw "machine"
    }
    if (@(Get-Process -Name "Hase.DesktopHost.App*", "Hase.Client.Wpf.App*" `
            -ErrorAction SilentlyContinue).Count -ne 0)
    {
        throw "processes"
    }

    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($PythonProfilePath))
    $journalPath = Join-Path $directory "python-credential-reissue.transaction.json"
    if (-not (Test-Path -LiteralPath $journalPath -PathType Leaf)) { throw "journal" }
    $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
    $entries = @($journal.Entries)
    if ($entries.Count -ne 4) { throw "journal" }
    Write-Host ("Re-issue journal phase      : {0}" -f [string]$journal.Phase)

    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
    $project = Join-Path $repository `
        "src\Hase.Python.CredentialProvisioning.Operator\Hase.Python.CredentialProvisioning.Operator.csproj"
    $arguments = @(
        "run", "--project", $project, "--configuration", "Release", "--verbosity", "quiet", "--",
        "reissue-recover",
        "--provisioning-directory", $directory,
        "--certificate", [string]$entries[0].TargetPath,
        "--private-key", [string]$entries[1].TargetPath,
        "--profile", [string]$entries[2].TargetPath,
        "--enrollment", [string]$entries[3].TargetPath,
        "--authorization-policy", [string]$journal.AuthorizationPolicyPath,
        "--expected-certificate-sha256", [string]$entries[0].OriginalSha256,
        "--expected-private-key-sha256", [string]$entries[1].OriginalSha256,
        "--expected-profile-sha256", [string]$entries[2].OriginalSha256,
        "--expected-enrollment-sha256", [string]$entries[3].OriginalSha256,
        "--expected-authorization-policy-sha256", [string]$journal.AuthorizationPolicySha256)
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "operator" }
    if (Test-Path -LiteralPath $journalPath) { throw "journal-remains" }
    Write-Host "Re-issue recovery complete  : True"
    exit 0
}
catch
{
    Write-Error "Python credential re-issue recovery failed; read the state before any further step."
    exit 1
}
