using Hase.DesktopHost.App.Hosting;
using System.IO;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaBindingStartupRequestTests
{
    [Fact]
    public void NormalStartupArguments_ShouldNotSelectBindingMode()
    {
        Assert.Null(RuntimeHostMediaBindingStartupRequest.Parse(
            ["C:\\HASE\\desktop-runtime-host.json"]));
    }

    [Fact]
    public void ExactBindingArguments_ShouldCreateRedactedRequest()
    {
        string path = Path.Combine(Path.GetTempPath(), "candidate.json");
        RuntimeHostMediaBindingStartupRequest? request =
            RuntimeHostMediaBindingStartupRequest.Parse(
            [
                RuntimeHostMediaBindingStartupRequest.Command,
                path,
                "camera-01",
                "generation-01",
                "Primary camera"
            ]);

        Assert.NotNull(request);
        Assert.Equal(Path.GetFullPath(path), request.OutputFilePath);
        Assert.DoesNotContain(path, request.ToString());
        Assert.DoesNotContain("generation-01", request.ToString());
    }

    [Theory]
    [InlineData("--prepare-media-binding")]
    [InlineData("--prepare-media-binding", "relative.json", "camera", "generation", "Camera")]
    public void InvalidBindingArguments_ShouldReject(params string[] arguments)
    {
        Assert.Throws<ArgumentException>(() =>
            RuntimeHostMediaBindingStartupRequest.Parse(arguments));
    }

    [Fact]
    public void BindingMode_ShouldBypassProductionBackendComposition()
    {
        string appSource = ReadAppSource();
        int bindingGuard = appSource.IndexOf(
            "if (mediaBindingRequest is not null)",
            StringComparison.Ordinal);
        int backendConstruction = appSource.IndexOf(
            "new ProductionPrivateNetworkRuntimeHostBackend",
            StringComparison.Ordinal);

        Assert.True(bindingGuard >= 0);
        Assert.True(backendConstruction > bindingGuard);
        Assert.Contains("return new RuntimeHostMediaBindingWindow", appSource);
    }

    private static string ReadAppSource(
        [CallerFilePath] string testSourceFilePath = "")
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testSourceFilePath)!,
            "..",
            ".."));
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Hase.DesktopHost.App",
            "App.xaml.cs"));
    }
}
