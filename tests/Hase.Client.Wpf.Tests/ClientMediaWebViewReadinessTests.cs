using System.IO;
using Hase.Client.Wpf.AppHost.Media;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientMediaWebViewReadinessTests
{
    [Fact]
    public async Task FirstWait_BlocksUntilReadySignalArrives()
    {
        var readiness = new ClientMediaWebViewReadiness(
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
        var readiness = new ClientMediaWebViewReadiness(
            TimeSpan.FromSeconds(1));

        readiness.Signal();
        ValueTask wait = readiness.WaitAsync(CancellationToken.None);

        Assert.True(wait.IsCompletedSuccessfully);
        await wait;
    }

    [Fact]
    public async Task RepeatedWaits_RemainReadyForCameraSwitching()
    {
        var readiness = new ClientMediaWebViewReadiness(
            TimeSpan.FromSeconds(1));
        readiness.Signal();

        await readiness.WaitAsync(CancellationToken.None);
        await readiness.WaitAsync(CancellationToken.None);

        Assert.True(readiness.IsReady);
    }

    [Fact]
    public async Task MissingReadySignal_TimesOut()
    {
        var readiness = new ClientMediaWebViewReadiness(
            TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await readiness.WaitAsync(CancellationToken.None));

        Assert.False(readiness.IsReady);
    }

    [Fact]
    public async Task CanceledWait_DoesNotPoisonLaterReadySignal()
    {
        var readiness = new ClientMediaWebViewReadiness(
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
            new ClientMediaWebViewReadiness(TimeSpan.Zero));
    }

    [Fact]
    public void PresentationBoundary_WaitsForReadyBeforeBeginningPresentation()
    {
        string source = ReadPresentationBoundarySource();
        int initialization = source.IndexOf(
            "await InitializeAsync(cancellationToken)",
            StringComparison.Ordinal);
        int readinessWait = source.IndexOf(
            "await readiness.WaitAsync(cancellationToken)",
            StringComparison.Ordinal);
        int active = source.IndexOf(
            "presentationActive = true",
            StringComparison.Ordinal);
        int beginCommand = source.IndexOf(
            "kind = \"begin-presentation\"",
            StringComparison.Ordinal);

        Assert.True(initialization >= 0);
        Assert.True(readinessWait > initialization);
        Assert.True(active > readinessWait);
        Assert.True(beginCommand > active);
    }

    [Fact]
    public void PresentationBoundary_ValidatedReadyMessageReleasesWait()
    {
        string source = ReadPresentationBoundarySource();

        Assert.Contains(
            "message!.Kind == ClientMediaWebMessageKind.Ready",
            source);
        Assert.Contains("readiness.Signal();", source);
        Assert.Contains(
            "ClientMediaWebMessageKind.PresentationFaulted",
            source);
        Assert.Contains("\"browser-failed\"", source);
    }

    private static string ReadPresentationBoundarySource()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string sourcePath = Path.Combine(
                directory.FullName,
                "src",
                "Hase.Client.Wpf.App",
                "Media",
                "WebView2ClientMediaPresentationBoundary.cs");
            if (File.Exists(sourcePath))
            {
                return File.ReadAllText(sourcePath);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
