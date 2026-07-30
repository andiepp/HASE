using System.IO;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.DesktopHost.App.Hosting;

public sealed record DesktopRuntimeHostStartupConfiguration(
    string DeploymentConfigurationFilePath,
    string Esp32Host,
    RuntimeHostPrivateNetworkDeploymentOptions DeploymentOptions,
    bool IncludeByteBufferSimulation = false,
    RuntimeDiagnosticLevel MaximumDiagnosticLevel =
        RuntimeDiagnosticLevel.Operational)
{
    public string PrivateNetworkBindingDisplay =>
        $"https://{DeploymentOptions.Binding.Address}:{DeploymentOptions.Binding.Port}";

    public static DesktopRuntimeHostStartupConfiguration Parse(
        IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        const string includeSimulationSwitch =
            "--include-byte-buffer-simulation";

        const string operationalDiagnosticsSwitch =
            "--diagnostics=operational";

        const string protocolDiagnosticsSwitch =
            "--diagnostics=protocol";

        const string byteDiagnosticsSwitch =
            "--diagnostics=bytes";

        if (commandLineArguments.Count is < 3 or > 5)
        {
            throw new ArgumentException(
                "Hase.DesktopHost.App requires the external desktop "
                + "private-network configuration file and ESP32 host name "
                + "or IP address, followed optionally by the simulation "
                + "switch and one diagnostics-level switch.",
                nameof(commandLineArguments));
        }

        bool includeByteBufferSimulation =
            false;

        RuntimeDiagnosticLevel maximumDiagnosticLevel =
            RuntimeDiagnosticLevel.Operational;

        bool diagnosticLevelSpecified =
            false;

        foreach (string option
            in commandLineArguments.Skip(3))
        {
            if (string.Equals(
                    option,
                    includeSimulationSwitch,
                    StringComparison.Ordinal))
            {
                if (includeByteBufferSimulation)
                {
                    throw CreateUnsupportedOptionsException(
                        commandLineArguments);
                }

                includeByteBufferSimulation =
                    true;

                continue;
            }

            RuntimeDiagnosticLevel? selectedLevel =
                option switch
                {
                    operationalDiagnosticsSwitch =>
                        RuntimeDiagnosticLevel.Operational,
                    protocolDiagnosticsSwitch =>
                        RuntimeDiagnosticLevel.Protocol,
                    byteDiagnosticsSwitch =>
                        RuntimeDiagnosticLevel.Bytes,
                    _ =>
                        null
                };

            if (selectedLevel is null
                || diagnosticLevelSpecified)
            {
                throw CreateUnsupportedOptionsException(
                    commandLineArguments);
            }

            maximumDiagnosticLevel =
                selectedLevel.Value;
            diagnosticLevelSpecified =
                true;
        }

        string configurationFilePath =
            commandLineArguments[1];
        string esp32Host =
            commandLineArguments[2];

        if (string.IsNullOrWhiteSpace(configurationFilePath))
        {
            throw new ArgumentException(
                "The external desktop private-network configuration file "
                + "must not be empty.",
                nameof(commandLineArguments));
        }

        if (string.IsNullOrWhiteSpace(esp32Host))
        {
            throw new ArgumentException(
                "The ESP32 host name or IP address must not be empty.",
                nameof(commandLineArguments));
        }

        string fullConfigurationFilePath =
            Path.GetFullPath(configurationFilePath);

        RuntimeHostPrivateNetworkDeploymentOptions deploymentOptions =
            RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                    fullConfigurationFilePath)
                .GetAwaiter()
                .GetResult();

        return new DesktopRuntimeHostStartupConfiguration(
            fullConfigurationFilePath,
            esp32Host.Trim(),
            deploymentOptions,
            includeByteBufferSimulation,
            maximumDiagnosticLevel);
    }

    private static ArgumentException CreateUnsupportedOptionsException(
        IReadOnlyList<string> commandLineArguments)
    {
        return new ArgumentException(
            "Optional arguments may contain "
            + "'--include-byte-buffer-simulation' once and one of "
            + "'--diagnostics=operational', '--diagnostics=protocol', "
            + "or '--diagnostics=bytes'.",
            nameof(commandLineArguments));
    }
}
