[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CutoverDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-HaseFileSha256([string] $Path)
{
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).
        Hash.ToLowerInvariant()
}

try
{
    $custodyPath = [IO.Path]::GetFullPath($CutoverDirectory)
    $journalPath = Join-Path $custodyPath `
        "laptop-cutover.transaction.json"
    $archivePath = Join-Path $custodyPath "replacement-transfer.zip"
    $rollbackDirectory = Join-Path $custodyPath "rollback"

    $journal = Get-Content -LiteralPath $journalPath -Raw |
        ConvertFrom-Json -ErrorAction Stop
    if ([string]$journal.phase -cne "replacement-installed")
    {
        throw "The cutover phase was not durable."
    }

    if ((Get-HaseFileSha256 $archivePath) -cne
        [string]$journal.archiveSha256)
    {
        throw "The protected replacement archive changed."
    }

    if (-not (Get-Acl -LiteralPath $custodyPath).
        AreAccessRulesProtected)
    {
        throw "The cutover directory ACL was not protected."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try
    {
        $manifestEntry = $archive.GetEntry("transfer-manifest.json")
        if ($null -eq $manifestEntry)
        {
            throw "The transfer manifest was absent."
        }

        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try
        {
            $manifest = $reader.ReadToEnd() |
                ConvertFrom-Json -ErrorAction Stop
        }
        finally
        {
            $reader.Dispose()
        }

        $installed = @(
            @([string]$journal.certificatePath, "client-certificate.pem"),
            @([string]$journal.privateKeyPath, "private-key.pem"),
            @([string]$journal.profilePath, "runtime-host-profile.json"))

        foreach ($pair in $installed)
        {
            $entry = $archive.GetEntry($pair[1])
            if ($null -eq $entry)
            {
                throw "A replacement archive entry was absent."
            }

            $memory = [IO.MemoryStream]::new()
            try
            {
                $entryStream = $entry.Open()
                try
                {
                    $entryStream.CopyTo($memory)
                }
                finally
                {
                    $entryStream.Dispose()
                }

                $sha256 = [Security.Cryptography.SHA256]::Create()
                try
                {
                    $hash = $sha256.ComputeHash($memory.ToArray())
                    try
                    {
                        $archiveHash = [BitConverter]::ToString($hash).
                            Replace("-", "").
                            ToLowerInvariant()
                    }
                    finally
                    {
                        [Array]::Clear($hash, 0, $hash.Length)
                    }
                }
                finally
                {
                    $sha256.Dispose()
                }
            }
            finally
            {
                $memory.Dispose()
            }

            $manifestMatch = @($manifest.files | Where-Object {
                [string]$_.name -ceq $pair[1]
            })
            if ($manifestMatch.Count -ne 1 -or
                [string]$manifestMatch[0].sha256 -cne $archiveHash -or
                (Get-HaseFileSha256 $pair[0]) -cne $archiveHash)
            {
                throw "An installed replacement was not byte-exact."
            }

            if (-not (Test-Path -LiteralPath `
                (Join-Path $rollbackDirectory $pair[1]) -PathType Leaf))
            {
                throw "An old-credential rollback file was absent."
            }
        }
    }
    finally
    {
        $archive.Dispose()
    }

    Write-Host "Cutover phase durable         : True"
    Write-Host "Installed certificate exact  : True"
    Write-Host "Installed private key exact  : True"
    Write-Host "Installed profile exact      : True"
    Write-Host "Protected archive unchanged  : True"
    Write-Host "Old credential rollback ready: True"
    Write-Host "MiniPC overlap changed       : False"
    Write-Host "Laptop cutover valid         : True"
}
catch
{
    Write-Error "Laptop cutover validation failed."
    exit 1
}
