using System.IO;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.DesktopHost.App.Hosting;

public sealed record DesktopRuntimeHostStartupConfiguration(
    string DeploymentConfigurationFilePath,
    string? Esp32Host,
    RuntimeHostPrivateNetworkDeploymentOptions DeploymentOptions,
    bool IncludeByteBufferSimulation = false,
    RuntimeDiagnosticLevel MaximumDiagnosticLevel = RuntimeDiagnosticLevel.Operational,
    bool RemoteDiagnosticsEnabled = false,
    RuntimeDiagnosticLevel RemoteDiagnosticsMaximumLevel =
        RuntimeDiagnosticLevel.Operational)
{
    public DesktopRuntimeHostInstallationProfile? InstallationProfile { get; init; }
    public DesktopRuntimeHostEndpointCompositionProfile? EndpointCompositionProfile { get; init; }

    public string PrivateNetworkBindingDisplay =>
        $"https://{DeploymentOptions.Binding.Address}:{DeploymentOptions.Binding.Port}";

    public static DesktopRuntimeHostStartupConfiguration Parse(IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        return commandLineArguments.Count == 2
            ? ParseApplicationProfile(commandLineArguments[1])
            : ParseLegacyArguments(commandLineArguments);
    }

    private static DesktopRuntimeHostStartupConfiguration ParseApplicationProfile(string applicationProfilePath)
    {
        if (string.IsNullOrWhiteSpace(applicationProfilePath)
            || !Path.IsPathFullyQualified(applicationProfilePath))
        {
            throw new ArgumentException(
                "The Desktop Runtime Host application-profile path must be fully qualified.",
                nameof(applicationProfilePath));
        }

        DesktopRuntimeHostInstallationProfile installation =
            DesktopRuntimeHostInstallationProfileFile.LoadAsync(Path.GetFullPath(applicationProfilePath))
                .GetAwaiter()
                .GetResult();
        DesktopRuntimeHostEndpointCompositionProfile endpoints =
            DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(installation.EndpointCompositionFilePath)
                .GetAwaiter()
                .GetResult();
        RuntimeHostPrivateNetworkDeploymentOptions deployment =
            RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(installation.PrivateNetworkConfigurationFilePath)
                .GetAwaiter()
                .GetResult();

        if (endpoints.NativeNetworkEndpoints.Count > 1)
        {
            throw new InvalidDataException(
                "The current production backend supports at most one native network endpoint.");
        }

        DesktopRuntimeHostNativeNetworkEndpointProfile? nativeEndpoint =
            endpoints.NativeNetworkEndpoints.SingleOrDefault();

        return new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            nativeEndpoint?.Host,
            deployment,
            installation.IncludeByteBufferSimulation,
            installation.MaximumDiagnosticLevel,
            installation.RemoteDiagnosticsEnabled,
            installation.RemoteDiagnosticsMaximumLevel)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = endpoints
        };
    }

    private static DesktopRuntimeHostStartupConfiguration ParseLegacyArguments(
        IReadOnlyList<string> commandLineArguments)
    {
        const string includeSimulationSwitch = "--include-byte-buffer-simulation";
        const string operationalDiagnosticsSwitch = "--diagnostics=operational";
        const string protocolDiagnosticsSwitch = "--diagnostics=protocol";
        const string byteDiagnosticsSwitch = "--diagnostics=bytes";

        if (commandLineArguments.Count is < 3 or > 5)
        {
            throw new ArgumentException(
                "Hase.DesktopHost.App requires one fully qualified application-profile path. The temporary Visual Studio compatibility form requires the private-network configuration and ESP32 host, followed by optional simulation and diagnostics switches.",
                nameof(commandLineArguments));
        }

        bool includeSimulation = false;
        RuntimeDiagnosticLevel diagnosticLevel = RuntimeDiagnosticLevel.Operational;
        bool diagnosticSpecified = false;

        foreach (string option in commandLineArguments.Skip(3))
        {
            if (option == includeSimulationSwitch && !includeSimulation)
            {
                includeSimulation = true;
                continue;
            }

            RuntimeDiagnosticLevel? selected = option switch
            {
                operationalDiagnosticsSwitch => RuntimeDiagnosticLevel.Operational,
                protocolDiagnosticsSwitch => RuntimeDiagnosticLevel.Protocol,
                byteDiagnosticsSwitch => RuntimeDiagnosticLevel.Bytes,
                _ => null
            };

            if (selected is null || diagnosticSpecified)
            {
                throw CreateUnsupportedOptionsException();
            }

            diagnosticLevel = selected.Value;
            diagnosticSpecified = true;
        }

        string configurationPath = commandLineArguments[1];
        string esp32Host = commandLineArguments[2];
        if (string.IsNullOrWhiteSpace(configurationPath) || string.IsNullOrWhiteSpace(esp32Host))
        {
            throw new ArgumentException("Legacy startup paths and host names must not be empty.", nameof(commandLineArguments));
        }

        string fullConfigurationPath = Path.GetFullPath(configurationPath);
        RuntimeHostPrivateNetworkDeploymentOptions deployment =
            RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(fullConfigurationPath)
                .GetAwaiter()
                .GetResult();

        return new DesktopRuntimeHostStartupConfiguration(
            fullConfigurationPath,
            esp32Host.Trim(),
            deployment,
            includeSimulation,
            diagnosticLevel);
    }

    private static ArgumentException CreateUnsupportedOptionsException() =>
        new("Optional legacy arguments may contain the simulation switch once and one diagnostics-level switch.");
}
