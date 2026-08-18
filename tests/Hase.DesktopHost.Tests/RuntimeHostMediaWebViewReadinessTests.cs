using System.IO;
using Hase.DesktopHost.App.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaWebViewReadinessTests
{
    [Fact]
    public async Task FirstWait_BlocksUntilReadySignalArrives()
    {
        var readiness = new RuntimeHostMediaWebViewReadiness(
            TimeSpan.FromSeconds(1));

        ValueTask wait = readiness.WaitAsync(CancellationToken.None);

        Assert.False(wait.IsCompleted);
        Assert.False(readiness.IsReady);

        readiness.Signal();
        await wait;

        Assert.True(readiness.IsReady);
    }

    [Fact]
    public async Task SignalBeforeFirstWait_CompletesImmediately()
    {
        var readiness = new RuntimeHostMediaWebViewReadiness(
            TimeSpan.FromSeconds(1));

        readiness.Signal();
        ValueTask wait = readiness.WaitAsync(CancellationToken.None);

        Assert.True(wait.IsCompletedSuccessfully);
        await wait;
    }

    [Fact]
    public async Task RepeatedWaits_RemainReadyForCameraSwitching()
    {
        var readiness = new RuntimeHostMediaWebViewReadiness(
            TimeSpan.FromSeconds(1));
        readiness.Signal();

        await readiness.WaitAsync(CancellationToken.None);
        await readiness.WaitAsync(CancellationToken.None);

        Assert.True(readiness.IsReady);
    }

    [Fact]
    public async Task MissingReadySignal_TimesOut()
    {
        var readiness = new RuntimeHostMediaWebViewReadiness(
            TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await readiness.WaitAsync(CancellationToken.None));

        Assert.False(readiness.IsReady);
    }

    [Fact]
    public async Task CanceledWait_DoesNotPoisonLaterReadySignal()
    {
        var readiness = new RuntimeHostMediaWebViewReadiness(
            TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await readiness.WaitAsync(cancellation.Token));

        readiness.Signal();
        await readiness.WaitAsync(CancellationToken.None);
        Assert.True(readiness.IsReady);
    }

    [Fact]
    public void NonPositiveTimeout_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeHostMediaWebViewReadiness(TimeSpan.Zero));
    }

    [Fact]
    public void CaptureBoundary_WaitsForReadyBeforeStartingCapture()
    {
        string source = ReadCaptureBoundarySource();
        int initialization = source.IndexOf(
            "await EnsureInitializedAsync()",
            StringComparison.Ordinal);
        int readinessWait = source.IndexOf(
            "await readiness.WaitAsync(cancellationToken)",
            StringComparison.Ordinal);
        int permission = source.IndexOf(
            "policy.BeginCapture(includeAudio)",
            StringComparison.Ordinal);
        int startCommand = source.IndexOf(
            "webView.CoreWebView2.PostWebMessageAsJson",
            StringComparison.Ordinal);

        Assert.True(initialization >= 0);
        Assert.True(readinessWait > initialization);
        Assert.True(permission > readinessWait);
        Assert.True(startCommand > permission);
    }

    [Fact]
    public void CaptureBoundary_ValidatedReadyMessageReleasesWait()
    {
        string source = ReadCaptureBoundarySource();

        Assert.Contains(
            "message!.Kind == RuntimeHostMediaWebMessageKind.Ready",
            source);
        Assert.Contains("readiness.Signal();", source);
        Assert.Contains(
            "RuntimeHostMediaWebMessageKind.CaptureFaulted",
            source);
        Assert.Contains("\"browser-failed\"", source);
    }

    private static string ReadCaptureBoundarySource()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string sourcePath = Path.Combine(
                directory.FullName,
                "src",
                "Hase.DesktopHost.App",
                "Media",
                "WebView2RuntimeHostMediaCaptureBoundary.cs");
            if (File.Exists(sourcePath))
            {
                return File.ReadAllText(sourcePath);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
