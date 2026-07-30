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
        var inventoryViewModel =
            new RuntimeInventoryViewModel(
                EmptyInventorySource.Instance);
        using var endpointDetailsViewModel =
            new EndpointDetailsViewModel(
                inventoryViewModel);
        var diagnosticsViewModel =
            new RuntimeDiagnosticsViewModel();
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                inventoryViewModel,
                endpointDetailsViewModel,
                new StubOperator(),
                diagnosticsViewModel:
                    diagnosticsViewModel);

        await viewModel.StartAsync();
        await viewModel.StopAsync();

        Assert.Equal(
            "HASE Desktop Runtime Host",
            viewModel.ApplicationTitle);
        Assert.Same(
            runtimeViewModel,
            viewModel.RuntimeHost);
        Assert.Same(
            inventoryViewModel,
            viewModel.Inventory);
        Assert.Same(
            endpointDetailsViewModel,
            viewModel.EndpointDetails);
        Assert.Same(
            diagnosticsViewModel,
            viewModel.Diagnostics);
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
            viewModel.Inventory.Endpoints);
        Assert.False(
            viewModel.EndpointDetails.HasSelection);
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

    private sealed class StubOperator
        : IDesktopRuntimeHostOperator
    {
        public Task<Hase.Runtime.Northbound.RuntimeHostPropertyOperationResult>
            ReadPropertyAsync(
                Hase.Runtime.Northbound.RuntimeHostPropertyTarget target,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Hase.Runtime.Northbound.RuntimeHostPropertyOperationResult>
            WritePropertyAsync(
                Hase.Runtime.Northbound.RuntimeHostPropertyTarget target,
                object? requestedValue,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Hase.Runtime.Northbound.RuntimeHostCommandOperationResult>
            ExecuteCommandAsync(
                Hase.Runtime.Northbound.RuntimeHostCommandTarget target,
                object? argument,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
