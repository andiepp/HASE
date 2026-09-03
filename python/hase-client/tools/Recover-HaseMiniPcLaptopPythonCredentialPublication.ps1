[CmdletBinding()]
param([Parameter(Mandatory = $true)] [string] $RollbackDirectory, [Parameter(Mandatory = $true)] [string] $ExpectedComputer)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

try
{
    if ($env:COMPUTERNAME -cne $ExpectedComputer) { throw "machine" }
    $rollback = [IO.Path]::GetFullPath($RollbackDirectory)
    $plan = Get-Content `
        -LiteralPath (Join-Path $rollback "transaction-plan.json") `
        -Raw | ConvertFrom-Json
    if ($plan.schemaVersion -ne 1 `
        -or $plan.purpose -cne "hase-minipc-laptop-python-credential-transaction")
    { throw "plan" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $entries = @($plan.entries)
    $staging = [string]($entries | Where-Object name -eq "stagingDirectory").path
    $certificate = [string]($entries | Where-Object name -eq "certificate").path
    $privateKey = [string]($entries | Where-Object name -eq "privateKey").path
    $profile = [string]($entries | Where-Object name -eq "pythonProfile").path
    $transfer = [string]($entries | Where-Object name -eq "transferArchive").path
    $enrollment = [string]($entries | Where-Object name -eq "enrollment").path
    $authorization = [string]($entries | Where-Object name -eq "authorizationPolicy").path
    $application = [string]($entries | Where-Object name -eq "applicationProfile").path
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $packageDirectory "..\.."))
    $operator = Join-Path `
        $repositoryRoot "src\Hase.Python.CredentialProvisioning.Operator"

    if (Test-Path -LiteralPath $staging -PathType Container)
    {
        $operatorJournals = @(Get-ChildItem `
            -LiteralPath $staging `
            -Filter ".hase-python-provisioning-*.journal.json*" `
            -File)
        if ($operatorJournals.Count -gt 0)
        {
            & dotnet run --project $operator -c Release -- recover `
                --provisioning-directory $staging `
                --certificate $certificate `
                --private-key $privateKey `
                --profile $profile `
                --enrollment $enrollment `
                --authorization-policy $authorization 1>$null 2>$null
            if ($LASTEXITCODE -ne 0) { throw "operator-recovery" }
        }
    }

    [IO.File]::WriteAllBytes($enrollment,
        [IO.File]::ReadAllBytes((Join-Path $rollback "enrollment.original")))
    [IO.File]::WriteAllBytes($authorization,
        [IO.File]::ReadAllBytes((Join-Path $rollback "authorization-policy.original")))
    [IO.File]::WriteAllBytes($application,
        [IO.File]::ReadAllBytes((Join-Path $rollback "application-profile.original")))
    if (Test-Path -LiteralPath $transfer) { Remove-Item -LiteralPath $transfer -Force }
    foreach ($file in @($certificate, $privateKey, $profile,
            (Join-Path $staging "transfer-manifest.json")))
    {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    if (Test-Path -LiteralPath $staging)
    {
        if (@(Get-ChildItem -LiteralPath $staging -Force).Count -ne 0)
        { throw "custody-not-empty" }
        Remove-Item -LiteralPath $staging -Force
    }
    $journal = Join-Path $rollback "publication-journal.json"
    if (Test-Path -LiteralPath $journal) { Remove-Item -LiteralPath $journal -Force }

    Write-Host "Enrollment restored           : True"
    Write-Host "Authorization policy restored : True"
    Write-Host "Application profile restored  : True"
    Write-Host "Laptop outputs absent         : True"
    Write-Host "Transfer package absent       : True"
    Write-Host "Preparation evidence retained : True"
    Write-Host "Publication recovery complete : True"
}
catch
{
    Write-Error "MiniPC Laptop Python credential publication recovery failed."
    exit 1
}
