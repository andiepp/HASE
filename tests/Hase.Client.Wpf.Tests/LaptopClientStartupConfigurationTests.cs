using Hase.Client.Wpf.AppHost;

namespace Hase.Client.Wpf.Tests;

public sealed class LaptopClientStartupConfigurationTests
{
    [Fact]
    public void Parse_ValidPath_ShouldExposeAbsolutePath()
    {
        LaptopClientStartupConfiguration configuration =
            LaptopClientStartupConfiguration.Parse(
                [
                    "Hase.Client.Wpf.App.exe",
                    "laptop-private-network.json"
                ]);

        Assert.Equal(
            Path.GetFullPath(
                "laptop-private-network.json"),
            configuration.ConfigurationFilePath);
    }

    [Fact]
    public void Parse_MissingPath_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => LaptopClientStartupConfiguration.Parse(
                [
                    "Hase.Client.Wpf.App.exe"
                ]));
    }

    [Fact]
    public void Parse_EmptyPath_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => LaptopClientStartupConfiguration.Parse(
                [
                    "Hase.Client.Wpf.App.exe",
                    " "
                ]));
    }
}
