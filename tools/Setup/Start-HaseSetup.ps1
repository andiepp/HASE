[CmdletBinding(DefaultParameterSetName = "Host")]
param(
    [Parameter(Mandatory, ParameterSetName = "Host")]
    [System.Net.IPAddress] $ListenerAddress,

    [Parameter(Mandatory, ParameterSetName = "Host")]
    [ValidateRange(1, 65535)]
    [int] $Port,

    [Parameter(Mandatory, ParameterSetName = "Host")]
    [string] $OutputDirectory,

    [Parameter(ParameterSetName = "Host")]
    [ValidateNotNullOrEmpty()]
    [string] $RuntimeHostId = "hase-example-host-01",

    [Parameter(ParameterSetName = "Host")]
    [ValidateNotNullOrEmpty()]
    [string] $ProfileId = "example-host",

    [Parameter(ParameterSetName = "Host")]
    [ValidateNotNullOrEmpty()]
    [string] $DisplayName = "Example Host (secured)",

    [Parameter(ParameterSetName = "Host")]
    [ValidateNotNullOrEmpty()]
    [string] $ClientPrincipalId = "laptop-validation-client",

    [Parameter(ParameterSetName = "Host")]
    [bool] $IncludeByteBufferSimulation = $true,

    [Parameter(ParameterSetName = "Host")]
    [ValidateNotNullOrEmpty()]
    [string] $BundleScriptPath = "",

    [Parameter(Mandatory, ParameterSetName = "Client")]
    [string] $BundleDirectory,

    [Parameter(ParameterSetName = "Client")]
    [ValidateNotNullOrEmpty()]
    [string] $InstallScriptPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne
    [System.PlatformID]::Win32NT) {
    throw "The HASE setup wizard requires Windows."
}

$repositoryRoot =
    [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine(
            $PSScriptRoot,
            "..",
            ".."))

$utf8WithoutBom =
    [System.Text.UTF8Encoding]::new($false)

function Write-WizardDocument {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Content
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        $utf8WithoutBom)
}

function Assert-TargetsAbsent {
    param(
        [Parameter(Mandatory)]
        [string[]] $Targets
    )

    foreach ($target in $Targets) {
        if ([System.IO.File]::Exists($target)) {
            throw "Setup refused because a target file already exists."
        }
    }
}

if ($PSCmdlet.ParameterSetName -eq "Host") {

    if ($BundleScriptPath -eq "") {
        $BundleScriptPath =
            [System.IO.Path]::Combine(
                $repositoryRoot,
                "tools",
                "PrivateNetwork",
                "New-HasePrivateNetworkValidationBundle.ps1")
    }

    if (-not [System.IO.File]::Exists($BundleScriptPath)) {
        throw "The provisioning bundle script was not found."
    }

    $outputPath =
        [System.IO.Path]::GetFullPath($OutputDirectory)

    $identityPath =
        [System.IO.Path]::Combine(
            $outputPath,
            "runtime-host-identity.json")
    $authorizationPolicyPath =
        [System.IO.Path]::Combine(
            $outputPath,
            "authorization-policy.json")
    $endpointCompositionPath =
        [System.IO.Path]::Combine(
            $outputPath,
            "desktop-runtime-endpoints.json")
    $installationProfilePath =
        [System.IO.Path]::Combine(
            $outputPath,
            "desktop-runtime-host.json")
    $clientHandoffPath =
        [System.IO.Path]::Combine(
            $outputPath,
            "client-handoff.json")
    $deploymentConfigurationPath =
        [System.IO.Path]::Combine(
            $outputPath,
            "desktop-private-network.json")

    Assert-TargetsAbsent -Targets @(
        $identityPath,
        $authorizationPolicyPath,
        $endpointCompositionPath,
        $installationProfilePath,
        $clientHandoffPath
    )

    Write-Host "Step 1 of 2: creating the credential bundle."
    Write-Host "The provisioning script prompts for a transfer password."

    & $BundleScriptPath `
        -ListenerAddress $ListenerAddress `
        -Port $Port `
        -OutputDirectory $outputPath `
        -ClientPrincipalId $ClientPrincipalId

    if (-not [System.IO.File]::Exists($deploymentConfigurationPath)) {
        throw "The provisioning bundle did not produce the expected files."
    }

    Write-Host "Step 2 of 2: authoring the Runtime Host documents."

    $escapedOutputPath =
        $outputPath.Replace("\", "\\")

    $identityDocument =
        "{`n" +
        "  `"formatVersion`": 1,`n" +
        "  `"runtimeHostId`": `"$RuntimeHostId`"`n" +
        "}`n"

    $grantLines =
        @(
            "runtime-host.snapshot.read",
            "property.cached.read",
            "property.authoritative.read",
            "property.write",
            "command.execute",
            "observation.subscribe"
        ) |
            ForEach-Object {
                "    { `"principalId`": `"$ClientPrincipalId`", " +
                "`"permission`": `"$_`" }"
            }
    $authorizationDocument =
        "{`n" +
        "  `"formatVersion`": 1,`n" +
        "  `"grants`": [`n" +
        (($grantLines) -join ",`n") + "`n" +
        "  ]`n" +
        "}`n"

    $simulationValue =
        if ($IncludeByteBufferSimulation) { "true" } else { "false" }

    $compositionDocument =
        "{`n" +
        "  `"formatVersion`": 1,`n" +
        "  `"endpoints`": [`n" +
        "    {`n" +
        "      `"kind`": `"CompactSerial`",`n" +
        "      `"expectedEndpointId`": `"arduino-uno-01`",`n" +
        "      `"vendorId`": 9025,`n" +
        "      `"productId`": 67,`n" +
        "      `"baudRate`": 115200,`n" +
        "      `"verificationTimeoutMilliseconds`": 3000`n" +
        "    }`n" +
        "  ]`n" +
        "}`n"

    $installationDocument =
        "{`n" +
        "  `"formatVersion`": 1,`n" +
        "  `"identityFilePath`": " +
        "`"$escapedOutputPath\\runtime-host-identity.json`",`n" +
        "  `"privateNetworkConfigurationFilePath`": " +
        "`"$escapedOutputPath\\desktop-private-network.json`",`n" +
        "  `"endpointCompositionFilePath`": " +
        "`"$escapedOutputPath\\desktop-runtime-endpoints.json`",`n" +
        "  `"authorizationPolicyFilePath`": " +
        "`"$escapedOutputPath\\authorization-policy.json`",`n" +
        "  `"includeByteBufferSimulation`": $simulationValue`n" +
        "}`n"

    $handoffDocument =
        "{`n" +
        "  `"formatVersion`": 1,`n" +
        "  `"profileId`": `"$ProfileId`",`n" +
        "  `"displayName`": `"$DisplayName`",`n" +
        "  `"expectedRuntimeHostId`": `"$RuntimeHostId`"`n" +
        "}`n"

    Write-WizardDocument -Path $identityPath -Content $identityDocument
    Write-WizardDocument -Path $authorizationPolicyPath -Content $authorizationDocument
    Write-WizardDocument -Path $endpointCompositionPath -Content $compositionDocument
    Write-WizardDocument -Path $installationProfilePath -Content $installationDocument
    Write-WizardDocument -Path $clientHandoffPath -Content $handoffDocument

    Write-Host ""
    Write-Host "Host setup complete."
    Write-Host ""
    Write-Host "Transfer these four files to the client PC:"
    Write-Host "  laptop-private-network.json"
    Write-Host "  laptop-client.pfx"
    Write-Host "  runtime-host-server.cer"
    Write-Host "  client-handoff.json"
    Write-Host ""
    Write-Host "Communicate the transfer password through a separate channel."
    Write-Host ""
    Write-Host ("Allow the port through the firewall in an elevated " +
        "window (right-click Start, Terminal (Admin)); without " +
        "elevation the rule is not created:")
    Write-Host ("  New-NetFirewallRule -DisplayName `"HASE Runtime Host " +
        "(secured)`" -Direction Inbound -Action Allow -Protocol TCP " +
        "-LocalPort $Port -Profile Private")
    Write-Host ""
    Write-Host "Start the Runtime Host from the repository root:"
    Write-Host ("  & `".\src\Hase.DesktopHost.App\bin\Release\" +
        "net10.0-windows\Hase.DesktopHost.App.exe`" `"$installationProfilePath`"")
    Write-Host ""
    Write-Host "The default endpoint composition expects the Example 1"
    Write-Host "Arduino Uno; edit desktop-runtime-endpoints.json for your"
    Write-Host "own endpoint mix before starting the Runtime Host."
}
else {

    if ($InstallScriptPath -eq "") {
        $InstallScriptPath =
            [System.IO.Path]::Combine(
                $repositoryRoot,
                "tools",
                "PrivateNetwork",
                "Install-HasePrivateNetworkValidationClient.ps1")
    }

    if (-not [System.IO.File]::Exists($InstallScriptPath)) {
        throw "The client installation script was not found."
    }

    $bundlePath =
        [System.IO.Path]::GetFullPath($BundleDirectory)

    $clientHandoffPath =
        [System.IO.Path]::Combine(
            $bundlePath,
            "client-handoff.json")
    $clientConfigurationPath =
        [System.IO.Path]::Combine(
            $bundlePath,
            "laptop-private-network.json")
    $registryPath =
        [System.IO.Path]::Combine(
            $bundlePath,
            "client-runtime-hosts.json")

    if (-not [System.IO.File]::Exists($clientHandoffPath)) {
        throw "The transfer package is incomplete: client-handoff.json is missing."
    }

    Assert-TargetsAbsent -Targets @(
        $registryPath
    )

    Write-Host "Step 1 of 2: installing the client credential."
    Write-Host "The installation script prompts for the transfer password."

    & $InstallScriptPath `
        -BundleDirectory $bundlePath

    Write-Host "Step 2 of 2: authoring the client registry."

    $handoff =
        Get-Content `
            -LiteralPath $clientHandoffPath `
            -Raw |
            ConvertFrom-Json

    foreach ($requiredProperty in @(
        "profileId",
        "displayName",
        "expectedRuntimeHostId"
    )) {
        if (-not ($handoff.PSObject.Properties.Name -contains
                $requiredProperty)) {
            throw "The client handoff document is incomplete."
        }
    }

    $escapedConfigurationPath =
        $clientConfigurationPath.Replace("\", "\\")

    $registryDocument =
        "{`n" +
        "  `"formatVersion`": 1,`n" +
        "  `"hosts`": [`n" +
        "    {`n" +
        "      `"profileId`": `"$($handoff.profileId)`",`n" +
        "      `"displayName`": `"$($handoff.displayName)`",`n" +
        "      `"expectedRuntimeHostId`": " +
        "`"$($handoff.expectedRuntimeHostId)`",`n" +
        "      `"enabled`": true,`n" +
        "      `"privateNetworkConfigurationFilePath`": " +
        "`"$escapedConfigurationPath`"`n" +
        "    }`n" +
        "  ]`n" +
        "}`n"

    Write-WizardDocument -Path $registryPath -Content $registryDocument

    Write-Host ""
    Write-Host "Client setup complete."
    Write-Host ""
    Write-Host "Securely delete laptop-client.pfx from every transfer"
    Write-Host "location now that the credential is installed."
    Write-Host ""
    Write-Host "Start the Client from the repository root:"
    Write-Host ("  & `".\src\Hase.Client.Wpf.App\bin\Release\" +
        "net10.0-windows\Hase.Client.Wpf.App.exe`" `"$registryPath`"")
}
