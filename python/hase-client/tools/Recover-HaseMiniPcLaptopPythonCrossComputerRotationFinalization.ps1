[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ProvisioningDirectory,
    [Parameter(Mandatory = $true)] [string] $EnrollmentPath,
    [Parameter(Mandatory = $true)] [string] $ExpectedComputer
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-HaseSha256([string] $Path)
{
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).
        Hash.ToLowerInvariant()
}

function Write-HaseExistingBytes([string] $Path, [byte[]] $Bytes)
{
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    try
    {
        $stream.SetLength(0)
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush($true)
    }
    finally
    {
        $stream.Dispose()
    }
}

$bytes = $null
try
{
    if ($env:OS -cne "Windows_NT" -or
        $env:COMPUTERNAME -cne $ExpectedComputer -or
        @(Get-Process -Name "Hase.DesktopHost.App","Hase.Client.Wpf.App" `
            -ErrorAction SilentlyContinue).Count -ne 0)
    {
        throw "preflight"
    }

    $custody = [IO.Path]::GetFullPath($ProvisioningDirectory)
    $enrollment = [IO.Path]::GetFullPath($EnrollmentPath)
    $journalPath = Join-Path $custody `
        "cross-computer-rotation.finalization.json"
    $journal = Get-Content -LiteralPath $journalPath -Raw |
        ConvertFrom-Json -ErrorAction Stop

    if ([string]$journal.phase -ceq "committed")
    {
        throw "Committed finalization cannot be implicitly rolled back."
    }

    if ([string]$journal.phase -cne "prepared" -or
        -not [bool]$journal.replacementConnectionProven)
    {
        throw "journal"
    }

    $backup = [IO.Path]::GetFullPath(
        [string]$journal.overlapBackupPath)
    if (-not (Test-Path -LiteralPath $backup -PathType Leaf))
    {
        throw "backup"
    }

    $access = (Get-Acl -LiteralPath $enrollment).
        GetSecurityDescriptorSddlForm(
            [Security.AccessControl.AccessControlSections]::Access)
    $bytes = [IO.File]::ReadAllBytes($backup)
    Write-HaseExistingBytes $enrollment $bytes

    if ((Get-HaseSha256 $enrollment) -cne
            [string]$journal.overlapEnrollmentSha256 -or
        (Get-Acl -LiteralPath $enrollment).
            GetSecurityDescriptorSddlForm(
                [Security.AccessControl.AccessControlSections]::Access) -cne
            $access)
    {
        throw "recovery-verification"
    }

    $journal.phase = "rolled-back"
    $utf8 = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText(
        $journalPath,
        ($journal | ConvertTo-Json -Depth 12),
        $utf8)

    Write-Host "Interrupted finalization recovered: True"
    Write-Host "Overlap enrollment restored       : True"
    Write-Host "Enrollment ACL unchanged          : True"
    Write-Host "Replacement connection proof kept : True"
    Write-Host "Deployment recovery complete      : True"
}
catch
{
    Write-Error "MiniPC cross-computer finalization recovery failed."
    exit 1
}
finally
{
    if ($null -ne $bytes)
    {
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}
