using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Diagnostics;

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
    public void Parse_WithUnsupportedOptionalArgument_ShouldRejectBeforeFileLoad()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "desktop-private-network.json",
                    "esp32.local",
                    "--unsupported"
                ]));
    }

    [Fact]
    public void DefaultsToOperationalDiagnostics()
    {
        DesktopRuntimeHostStartupConfiguration configuration =
            new(
                "configuration.json",
                "esp32.local",
                null!);

        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            configuration.MaximumDiagnosticLevel);
    }

    [Fact]
    public void Parse_WithDuplicateDiagnosticLevel_ShouldRejectBeforeFileLoad()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "desktop-private-network.json",
                    "esp32.local",
                    "--diagnostics=protocol",
                    "--diagnostics=bytes"
                ]));
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
