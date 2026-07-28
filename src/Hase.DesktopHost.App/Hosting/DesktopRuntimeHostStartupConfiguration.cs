using System.IO;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.DesktopHost.App.Hosting;

public sealed record DesktopRuntimeHostStartupConfiguration(
    string DeploymentConfigurationFilePath,
    string Esp32Host,
    RuntimeHostPrivateNetworkDeploymentOptions DeploymentOptions,
    bool IncludeByteBufferSimulation = false)
{
    public string PrivateNetworkBindingDisplay =>
        $"https://{DeploymentOptions.Binding.Address}:{DeploymentOptions.Binding.Port}";

    public static DesktopRuntimeHostStartupConfiguration Parse(
        IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        const string includeSimulationSwitch =
            "--include-byte-buffer-simulation";

        if (commandLineArguments.Count is < 3 or > 4)
        {
            throw new ArgumentException(
                "Hase.DesktopHost.App requires the external desktop "
                + "private-network configuration file and ESP32 host name "
                + "or IP address, followed optionally by "
                + $"'{includeSimulationSwitch}'.",
                nameof(commandLineArguments));
        }

        bool includeByteBufferSimulation =
            commandLineArguments.Count == 4
            && string.Equals(
                commandLineArguments[3],
                includeSimulationSwitch,
                StringComparison.Ordinal);

        if (commandLineArguments.Count == 4
            && !includeByteBufferSimulation)
        {
            throw new ArgumentException(
                $"The only supported optional argument is "
                + $"'{includeSimulationSwitch}'.",
                nameof(commandLineArguments));
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
            includeByteBufferSimulation);
    }
}

