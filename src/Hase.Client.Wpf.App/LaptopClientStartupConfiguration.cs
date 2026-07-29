using System.IO;

namespace Hase.Client.Wpf.AppHost;

public sealed record LaptopClientStartupConfiguration(
    string ConfigurationFilePath)
{
    public static LaptopClientStartupConfiguration Parse(
        IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(
            commandLineArguments);

        if (commandLineArguments.Count != 2)
        {
            throw new ArgumentException(
                "Hase.Client.Wpf.App requires exactly one argument: "
                + "the laptop private-network configuration file.",
                nameof(commandLineArguments));
        }

        string configurationFilePath =
            commandLineArguments[1];

        if (string.IsNullOrWhiteSpace(
                configurationFilePath))
        {
            throw new ArgumentException(
                "The laptop private-network configuration file path "
                + "must not be empty.",
                nameof(commandLineArguments));
        }

        return new LaptopClientStartupConfiguration(
            Path.GetFullPath(
                configurationFilePath));
    }
}
