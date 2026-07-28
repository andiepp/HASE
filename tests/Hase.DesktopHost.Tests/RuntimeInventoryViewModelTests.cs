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

    [Theory]
    [InlineData("Ready", true, false, false, false, "● Ready")]
    [InlineData("Connecting", false, true, false, false, "◐ Connecting")]
    [InlineData("Synchronizing", false, true, false, false, "◐ Synchronizing")]
    [InlineData("Reconnecting", false, true, false, false, "◐ Reconnecting")]
    [InlineData("Faulted", false, false, true, false, "⚠ Faulted")]
    [InlineData("Disconnected", false, false, false, true, "○ Disconnected")]
    public void EndpointState_ShouldExposePresentationFlags(
        string state,
        bool isReady,
        bool isRecovering,
        bool isFaulted,
        bool isDisconnected,
        string indicatorText)
    {
        var viewModel =
            new DesktopRuntimeEndpointViewModel(
                "endpoint-1",
                "Endpoint",
                state,
                Guid.NewGuid().ToString());

        Assert.Equal(
            isReady,
            viewModel.IsReady);
        Assert.Equal(
            isRecovering,
            viewModel.IsRecovering);
        Assert.Equal(
            isFaulted,
            viewModel.IsFaulted);
        Assert.Equal(
            isDisconnected,
            viewModel.IsDisconnected);
        Assert.Equal(
            indicatorText,
            viewModel.StateIndicatorText);
    }

    [Fact]
    public void Update_ShouldNotifyAllConnectionStatePresentationProperties()
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
                    "Faulted"
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
        Assert.Contains(
            nameof(DesktopRuntimeEndpointViewModel.IsFaulted),
            changedProperties);
        Assert.Contains(
            nameof(DesktopRuntimeEndpointViewModel.IsDisconnected),
            changedProperties);
        Assert.Contains(
            nameof(DesktopRuntimeEndpointViewModel.StateIndicatorText),
            changedProperties);
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
