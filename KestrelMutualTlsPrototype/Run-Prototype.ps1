[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$prototypeRoot = $PSScriptRoot
$artifactsDirectory = Join-Path $prototypeRoot 'artifacts'
$certificateDirectory = Join-Path $artifactsDirectory 'certificates'
$serverOutputPath = Join-Path $artifactsDirectory 'server.stdout.log'
$serverErrorPath = Join-Path $artifactsDirectory 'server.stderr.log'
$serverProject = Join-Path $prototypeRoot 'src\KestrelMtls.Server\KestrelMtls.Server.csproj'
$clientProject = Join-Path $prototypeRoot 'src\KestrelMtls.Client\KestrelMtls.Client.csproj'
$certificateProject = Join-Path $prototypeRoot 'src\KestrelMtls.Certificates\KestrelMtls.Certificates.csproj'
$solutionPath = Join-Path $prototypeRoot 'KestrelMutualTlsPrototype.slnx'

New-Item -ItemType Directory -Force -Path $artifactsDirectory | Out-Null

Write-Host 'Building the prototype.'
dotnet build $solutionPath
if ($LASTEXITCODE -ne 0) {
    throw "Prototype build failed with exit code $LASTEXITCODE."
}

Write-Host ''
Write-Host 'Generating isolated test certificates.'
dotnet run --no-build --project $certificateProject -- $certificateDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Certificate generation failed with exit code $LASTEXITCODE."
}

Remove-Item -Force -ErrorAction SilentlyContinue $serverOutputPath, $serverErrorPath

$serverProcess = $null

try {
    Write-Host ''
    Write-Host 'Starting the Kestrel server.'
    $serverProcess = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @(
            'run',
            '--no-build',
            '--project',
            $serverProject,
            '--',
            $certificateDirectory
        ) `
        -PassThru `
        -RedirectStandardOutput $serverOutputPath `
        -RedirectStandardError $serverErrorPath

    $ready = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if ($serverProcess.HasExited) {
            throw "Kestrel exited before becoming ready with exit code $($serverProcess.ExitCode)."
        }

        if ((Test-Path $serverOutputPath) -and
            (Select-String -Quiet -Path $serverOutputPath -SimpleMatch 'P-001 server ready')) {
            $ready = $true
            break
        }

        Start-Sleep -Milliseconds 200
        $serverProcess.Refresh()
    }

    if (-not $ready) {
        throw 'Kestrel did not become ready within 10 seconds.'
    }

    Write-Host ''
    Write-Host 'Running the authenticated HTTP/2 client.'
    dotnet run --no-build --project $clientProject -- $certificateDirectory authenticated
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticated prototype client failed with exit code $LASTEXITCODE."
    }

    Write-Host ''
    Write-Host 'Running the HTTP/2 client without a client certificate.'
    dotnet run --no-build --project $clientProject -- $certificateDirectory missing
    if ($LASTEXITCODE -ne 0) {
        throw "Missing-certificate prototype client failed with exit code $LASTEXITCODE."
    }

    Write-Host ''
    Write-Host 'Running the HTTP/2 client with an untrusted client certificate.'
    dotnet run --no-build --project $clientProject -- $certificateDirectory untrusted
    if ($LASTEXITCODE -ne 0) {
        throw "Untrusted-certificate prototype client failed with exit code $LASTEXITCODE."
    }

    Write-Host ''
    Write-Host 'Running the authenticated unary gRPC client.'
    dotnet run --no-build --project $clientProject -- $certificateDirectory grpc
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticated gRPC prototype client failed with exit code $LASTEXITCODE."
    }

    Start-Sleep -Milliseconds 500
    $probeExecutions = @(
        Select-String `
            -Path $serverOutputPath `
            -SimpleMatch 'Probe endpoint execution count:'
    ).Count

    if ($probeExecutions -ne 1) {
        throw "Expected exactly one probe endpoint execution, but observed $probeExecutions."
    }

    $untrustedRejections = @(
        Select-String `
            -Path $serverOutputPath `
            -SimpleMatch 'Rejected client certificate: CN=HASE Kestrel Prototype Untrusted Client'
    ).Count

    if ($untrustedRejections -ne 1) {
        throw "Expected exactly one explicit untrusted-certificate rejection, but observed $untrustedRejections."
    }

    $grpcProbeExecutions = @(
        Select-String `
            -Path $serverOutputPath `
            -SimpleMatch 'gRPC probe execution count:'
    ).Count

    if ($grpcProbeExecutions -ne 1) {
        throw "Expected exactly one gRPC probe execution, but observed $grpcProbeExecutions."
    }

    Write-Host ''
    Write-Host 'Combined result'
    Write-Host '==============='
    Write-Host 'P-001 authenticated client : PASS'
    Write-Host 'P-002 missing certificate  : PASS'
    Write-Host 'P-003 untrusted client     : PASS'
    Write-Host 'P-004 authenticated gRPC   : PASS'
    Write-Host "HTTP probe executions      : $probeExecutions"
    Write-Host "gRPC probe executions      : $grpcProbeExecutions"
    Write-Host "Untrusted TLS rejections   : $untrustedRejections"
}
finally {
    if (($null -ne $serverProcess) -and (-not $serverProcess.HasExited)) {
        Stop-Process -Id $serverProcess.Id
        $serverProcess.WaitForExit()
    }

    Write-Host ''
    Write-Host 'Kestrel server output'
    Write-Host '====================='
    if (Test-Path $serverOutputPath) {
        Get-Content $serverOutputPath
    }

    if ((Test-Path $serverErrorPath) -and
        ((Get-Item $serverErrorPath).Length -gt 0)) {
        Write-Host ''
        Write-Host 'Kestrel server errors'
        Write-Host '====================='
        Get-Content $serverErrorPath
    }
}
