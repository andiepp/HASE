using Hase.DesktopHost.App.Hosting;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostStartupConfigurationTests
{
    [Fact]
    public void Parse_WithWrongArgumentCount_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                ["Hase.DesktopHost.App.exe"]));
    }

    [Fact]
    public void Parse_WithEmptyConfigurationPath_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    " ",
                    "esp32.local"
                ]));
    }

    [Fact]
    public void Parse_WithEmptyEsp32Host_ShouldRejectBeforeFileLoad()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "desktop-private-network.json",
                    " "
                ]));
    }
}
