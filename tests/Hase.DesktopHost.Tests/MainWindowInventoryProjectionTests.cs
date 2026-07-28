using System.ComponentModel;
using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class MainWindowInventoryProjectionTests
{
    [Fact]
    public void RefreshInventory_ShouldProjectAndSortEndpoints()
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
                        "z-endpoint",
                        "Zulu",
                        "Ready",
                        Guid.NewGuid().ToString()),
                    new DesktopRuntimeEndpointSnapshot(
                        "a-endpoint",
                        "Alpha",
                        "Reconnecting",
                        Guid.NewGuid().ToString())
                ]);
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                inventorySource);

        viewModel.RefreshInventory();

        Assert.Equal(
            2,
            viewModel.PublishedEndpointCount);
        Assert.Equal(
            "Alpha",
            viewModel.Endpoints[0].DisplayName);
        Assert.Equal(
            "Reconnecting",
            viewModel.Endpoints[0].ConnectionState);
        Assert.Equal(
            "Zulu",
            viewModel.Endpoints[1].DisplayName);
    }

    [Fact]
    public void RefreshInventory_ShouldReplacePreviousProjection()
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
            new MutableInventorySource();
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                inventorySource);

        inventorySource.Snapshots =
        [
            new DesktopRuntimeEndpointSnapshot(
                "endpoint-1",
                "First",
                "Ready",
                Guid.NewGuid().ToString())
        ];
        viewModel.RefreshInventory();

        inventorySource.Snapshots =
        [
            new DesktopRuntimeEndpointSnapshot(
                "endpoint-2",
                "Second",
                "Faulted",
                Guid.NewGuid().ToString())
        ];
        viewModel.RefreshInventory();

        Assert.Single(
            viewModel.Endpoints);
        Assert.Equal(
            "endpoint-2",
            viewModel.Endpoints[0].EndpointId);
        Assert.Equal(
            "Faulted",
            viewModel.Endpoints[0].ConnectionState);
    }

    [Fact]
    public void RefreshInventory_ShouldNotifyPublishedEndpointCount()
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
            new MutableInventorySource
            {
                Snapshots =
                [
                    new DesktopRuntimeEndpointSnapshot(
                        "endpoint-1",
                        "First",
                        "Ready",
                        Guid.NewGuid().ToString()),
                    new DesktopRuntimeEndpointSnapshot(
                        "endpoint-2",
                        "Second",
                        "Ready",
                        Guid.NewGuid().ToString())
                ]
            };
        using var viewModel =
            new MainWindowViewModel(
                runtimeViewModel,
                inventorySource);
        var changedProperties =
            new List<string?>();

        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.RefreshInventory();

        Assert.Contains(
            nameof(MainWindowViewModel.PublishedEndpointCount),
            changedProperties);
        Assert.Equal(
            2,
            viewModel.PublishedEndpointCount);
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

    private sealed class MutableInventorySource
        : IDesktopRuntimeHostInventorySource
    {
        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Snapshots
        {
            get;
            set;
        } =
            [];

        public IReadOnlyList<DesktopRuntimeEndpointSnapshot> Capture() =>
            Snapshots;
    }
}
