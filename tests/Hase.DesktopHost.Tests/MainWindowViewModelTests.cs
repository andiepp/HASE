using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task StartAndStopAsync_ShouldDelegateToRuntimeProjection()
    {
        var backend =
            new RecordingBackend();
        var host =
            new DesktopRuntimeHost(
                backend);
        using var runtimeViewModel =
            new DesktopRuntimeHostViewModel(
                host,
                new DesktopRuntimeHostShellInformation(
                    "Shell validation backend",
                    "Not available",
                    "Not available",
                    "Not configured",
                    "Not configured"));
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                EmptyInventorySource.Instance);

        await viewModel.StartAsync();
        await viewModel.StopAsync();

        Assert.Equal(
            "HASE Desktop Runtime Host",
            viewModel.ApplicationTitle);
        Assert.Same(
            runtimeViewModel,
            viewModel.RuntimeHost);
        Assert.Equal(
            1,
            backend.StartCount);
        Assert.Equal(
            1,
            backend.StopCount);
        Assert.Equal(
            DesktopRuntimeHostStatus.Stopped,
            viewModel.RuntimeHost.Status);
        Assert.Empty(
            viewModel.Endpoints);
        Assert.Equal(
            0,
            viewModel.PublishedEndpointCount);
    }

    private sealed class RecordingBackend
        : IDesktopRuntimeHostBackend
    {
        public int StartCount
        {
            get;
            private set;
        }

        public int StopCount
        {
            get;
            private set;
        }

        public Task StartAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyInventorySource
        : IDesktopRuntimeHostInventorySource
    {
        public static EmptyInventorySource Instance
        {
            get;
        } =
            new();

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture() =>
            [];
    }
}
