using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class MainWindowInventoryProjectionTests
{
    [Fact]
    public void RefreshInventory_ShouldDelegateToInventoryAndDetailsViewModels()
    {
        var backend =
            new StubBackend();
        var runtimeHost =
            new DesktopRuntimeHost(
                backend);
        var runtimeViewModel =
            new DesktopRuntimeHostViewModel(
                runtimeHost,
                new DesktopRuntimeHostShellInformation(
                    "composition",
                    "host",
                    "1.0",
                    "loopback",
                    "private"));
        var inventorySource =
            new StubInventorySource(
                [
                    new DesktopRuntimeEndpointSnapshot(
                        "endpoint-1",
                        "Endpoint",
                        "Ready",
                        Guid.NewGuid().ToString())
                ]);
        var inventoryViewModel =
            new RuntimeInventoryViewModel(
                inventorySource);
        using var endpointDetailsViewModel =
            new EndpointDetailsViewModel(
                inventoryViewModel);
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                inventoryViewModel,
                endpointDetailsViewModel,
                new StubOperator(),
                backend);

        viewModel.RefreshInventory();

        Assert.Single(
            viewModel.Inventory.Endpoints);
        Assert.Equal(
            1,
            viewModel.Inventory.PublishedEndpointCount);
        Assert.Equal(
            "endpoint-1",
            viewModel.Inventory.Endpoints[0].EndpointId);
        Assert.Same(
            viewModel.Inventory.Endpoints[0],
            viewModel.Inventory.SelectedEndpoint);
        Assert.True(
            viewModel.EndpointDetails.HasSelection);
        Assert.Equal(
            "endpoint-1",
            viewModel.EndpointDetails.EndpointId);
        Assert.Equal(
            "Endpoint",
            viewModel.EndpointDetails.DisplayName);
        Assert.Equal(
            "Ready",
            viewModel.EndpointDetails.ConnectionState);
    }

    private sealed class StubBackend
        : IDesktopRuntimeHostBackend,
          IDesktopRuntimeHostEndpointRefresher
    {
        public Task StartAsync(
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StopAsync(
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RefreshEndpointsAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubInventorySource
        : IDesktopRuntimeHostInventorySource
    {
        private readonly IReadOnlyList<DesktopRuntimeEndpointSnapshot>
            snapshots;

        public StubInventorySource(
            IReadOnlyList<DesktopRuntimeEndpointSnapshot> snapshots)
        {
            this.snapshots =
                snapshots;
        }

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture() =>
            snapshots;
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
