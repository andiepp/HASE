using System.IO;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.DesktopHost.App.Hosting;

public sealed record DesktopRuntimeHostStartupConfiguration(
    string DeploymentConfigurationFilePath,
    string Esp32Host,
    RuntimeHostPrivateNetworkDeploymentOptions DeploymentOptions)
{
    public string PrivateNetworkBindingDisplay =>
        $"https://{DeploymentOptions.Binding.Address}:{DeploymentOptions.Binding.Port}";

    public static DesktopRuntimeHostStartupConfiguration Parse(
        IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        if (commandLineArguments.Count != 3)
        {
            throw new ArgumentException(
                "Hase.DesktopHost.App requires exactly two arguments: "
                + "the external desktop private-network configuration file "
                + "and the ESP32 host name or IP address.",
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
            deploymentOptions);
    }
}
