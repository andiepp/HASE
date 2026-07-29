using Hase.Client.Wpf.AppHost;

namespace Hase.Client.Wpf.Tests;

public sealed class StartupClientConfigurationFilePickerTests
{
    [Fact]
    public void PickConfigurationFile_ShouldReturnStartupPath()
    {
        string configurationFilePath =
            Path.GetFullPath(
                "laptop-private-network.json");
        var picker =
            new StartupClientConfigurationFilePicker(
                new LaptopClientStartupConfiguration(
                    configurationFilePath));

        Assert.Equal(
            configurationFilePath,
            picker.PickConfigurationFile());
    }

    [Fact]
    public void Constructor_NullConfiguration_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "configuration",
            () => new StartupClientConfigurationFilePicker(
                null!));
    }
}
