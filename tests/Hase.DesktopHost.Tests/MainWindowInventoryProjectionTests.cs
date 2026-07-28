using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class MainWindowInventoryProjectionTests
{
    [Fact]
    public void RefreshInventory_ShouldDelegateToInventoryViewModel()
    {
        var runtimeHost =
            new DesktopRuntimeHost(
                new StubBackend());
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
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                inventoryViewModel);

        viewModel.RefreshInventory();

        Assert.Single(
            viewModel.Inventory.Endpoints);
        Assert.Equal(
            1,
            viewModel.Inventory.PublishedEndpointCount);
        Assert.Equal(
            "endpoint-1",
            viewModel.Inventory.Endpoints[0].EndpointId);
    }

    private sealed class StubBackend
        : IDesktopRuntimeHostBackend
    {
        public Task StartAsync(
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StopAsync(
            CancellationToken cancellationToken) =>
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
}
