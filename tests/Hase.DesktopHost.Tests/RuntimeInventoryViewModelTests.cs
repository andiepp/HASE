using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeInventoryViewModelTests
{
    [Fact]
    public void Refresh_ShouldPreserveExistingEndpointViewModel()
    {
        var source =
            new MutableInventorySource
            {
                Snapshots =
                [
                    CreateSnapshot(
                        "endpoint-1",
                        "Endpoint",
                        "Ready")
                ]
            };
        var viewModel =
            new RuntimeInventoryViewModel(
                source);

        viewModel.Refresh();
        DesktopRuntimeEndpointViewModel original =
            viewModel.Endpoints[0];

        source.Snapshots =
        [
            CreateSnapshot(
                "endpoint-1",
                "Endpoint",
                "Reconnecting")
        ];
        viewModel.Refresh();

        Assert.Same(
            original,
            viewModel.Endpoints[0]);
        Assert.Equal(
            "Reconnecting",
            original.ConnectionState);
    }

    [Fact]
    public void Refresh_ShouldAddRemoveAndSortEndpoints()
    {
        var source =
            new MutableInventorySource
            {
                Snapshots =
                [
                    CreateSnapshot(
                        "z-endpoint",
                        "Zulu",
                        "Ready"),
                    CreateSnapshot(
                        "a-endpoint",
                        "Alpha",
                        "Ready")
                ]
            };
        var viewModel =
            new RuntimeInventoryViewModel(
                source);

        viewModel.Refresh();

        Assert.Equal(
            ["a-endpoint", "z-endpoint"],
            viewModel.Endpoints
                .Select(
                    endpoint =>
                        endpoint.EndpointId)
                .ToArray());

        source.Snapshots =
        [
            CreateSnapshot(
                "z-endpoint",
                "Zulu",
                "Faulted")
        ];
        viewModel.Refresh();

        Assert.Single(
            viewModel.Endpoints);
        Assert.Equal(
            "z-endpoint",
            viewModel.Endpoints[0].EndpointId);
        Assert.Equal(
            "Faulted",
            viewModel.Endpoints[0].ConnectionState);
    }

    [Fact]
    public void Update_ShouldNotifyConnectionStateFlags()
    {
        DesktopRuntimeEndpointSnapshot initial =
            CreateSnapshot(
                "endpoint-1",
                "Endpoint",
                "Ready");
        var viewModel =
            new DesktopRuntimeEndpointViewModel(
                initial.EndpointId,
                initial.DisplayName,
                initial.ConnectionState,
                initial.AttachmentGeneration);
        var changedProperties =
            new List<string?>();

        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.Update(
            initial with
            {
                ConnectionState =
                    "Reconnecting"
            });

        Assert.Contains(
            nameof(DesktopRuntimeEndpointViewModel.ConnectionState),
            changedProperties);
        Assert.Contains(
            nameof(DesktopRuntimeEndpointViewModel.IsReady),
            changedProperties);
        Assert.Contains(
            nameof(DesktopRuntimeEndpointViewModel.IsRecovering),
            changedProperties);
        Assert.False(
            viewModel.IsReady);
        Assert.True(
            viewModel.IsRecovering);
    }

    private static DesktopRuntimeEndpointSnapshot CreateSnapshot(
        string endpointId,
        string displayName,
        string connectionState) =>
        new(
            endpointId,
            displayName,
            connectionState,
            Guid.NewGuid().ToString());

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
