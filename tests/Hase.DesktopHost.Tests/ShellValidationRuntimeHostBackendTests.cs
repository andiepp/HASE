using Hase.DesktopHost.App.Hosting;

namespace Hase.DesktopHost.Tests;

public sealed class ShellValidationRuntimeHostBackendTests
{
    [Fact]
    public async Task StartAndStopAsync_ShouldCompleteWithoutStartingProductionServices()
    {
        var backend = new ShellValidationRuntimeHostBackend();

        await backend.StartAsync(CancellationToken.None);
        await backend.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_ShouldThrow()
    {
        var backend = new ShellValidationRuntimeHostBackend();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => backend.StartAsync(cancellation.Token));
    }
}
