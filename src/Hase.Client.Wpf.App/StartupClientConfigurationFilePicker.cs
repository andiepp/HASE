using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.AppHost;

public sealed class StartupClientConfigurationFilePicker
    : IClientConfigurationFilePicker
{
    private readonly LaptopClientStartupConfiguration configuration;

    public StartupClientConfigurationFilePicker(
        LaptopClientStartupConfiguration configuration)
    {
        this.configuration =
            configuration
            ?? throw new ArgumentNullException(
                nameof(configuration));
    }

    public string PickConfigurationFile()
    {
        return configuration.ConfigurationFilePath;
    }
}
