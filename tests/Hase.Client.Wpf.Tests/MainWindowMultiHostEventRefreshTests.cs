using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowMultiHostEventRefreshTests
{
    [Fact]
    public void SameHostObservationRefresh_AfterEvent_ShouldPreserveOccurrence()
    {
        RuntimeHostProfile profile =
            new(
                new RuntimeHostProfileId("minipc"),
                "MiniPC",
                new RemoteRuntimeHostId("minipc-host"));
        RemoteObservationState initialState =
            CreateState(
                profile.ExpectedRuntimeHostId);
        var viewModel =
            new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(
            new RuntimeHostProfileRegistry(
                [profile]));
        viewModel.ApplyMultiHostSnapshot(
            Snapshot(
                profile,
                RuntimeHostClientSessionState.Connected,
                initialState));
        viewModel.SelectRuntimeHost(
            profile.ProfileId);
        RemoteRuntimeHostObservation observation =
            CreateEventObservation(
                initialState);

        viewModel.ApplyMultiHostEventOccurred(
            new RuntimeHostProfileEventOccurredEventArgs(
                profile.ProfileId,
                profile.ExpectedRuntimeHostId,
                observation));
        viewModel.ApplyMultiHostSnapshot(
            Snapshot(
                profile,
                RuntimeHostClientSessionState.Connected,
                new RemoteObservationReducer().Apply(
                    initialState,
                    observation)));

        EventOccurrenceItemViewModel occurrence =
            Assert.Single(
                viewModel.EventOccurrences);
        Assert.Equal(
            "Button Pressed",
            occurrence.DisplayName);
    }

    [Fact]
    public void NewConnectionBoundary_AfterEvent_ShouldClearOccurrence()
    {
        RuntimeHostProfile profile =
            new(
                new RuntimeHostProfileId("minipc"),
                "MiniPC",
                new RemoteRuntimeHostId("minipc-host"));
        RemoteObservationState state =
            CreateState(
                profile.ExpectedRuntimeHostId);
        var viewModel =
            new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(
            new RuntimeHostProfileRegistry(
                [profile]));
        viewModel.ApplyMultiHostSnapshot(
            Snapshot(
                profile,
                RuntimeHostClientSessionState.Connected,
                state));
        viewModel.SelectRuntimeHost(
            profile.ProfileId);
        viewModel.ApplyMultiHostEventOccurred(
            new RuntimeHostProfileEventOccurredEventArgs(
                profile.ProfileId,
                profile.ExpectedRuntimeHostId,
                CreateEventObservation(
                    state)));

        viewModel.ApplyMultiHostSnapshot(
            Snapshot(
                profile,
                RuntimeHostClientSessionState.Reconnecting,
                state));
        viewModel.ApplyMultiHostSnapshot(
            Snapshot(
                profile,
                RuntimeHostClientSessionState.Connected,
                state));

        Assert.Empty(
            viewModel.EventOccurrences);
    }

    private static MultiHostClientSessionSnapshot Snapshot(
        RuntimeHostProfile profile,
        RuntimeHostClientSessionState sessionState,
        RemoteObservationState state)
    {
        var status =
            new RuntimeHostClientSessionStatus(
                sessionState,
                profile.ExpectedRuntimeHostId,
                RuntimeHostClientApiVersion.Current);
        return new MultiHostClientSessionSnapshot(
            [
                new RuntimeHostProfileSessionSnapshot(
                    profile,
                    status,
                    DateTimeOffset.UtcNow,
                    state)
            ]);
    }

    private static RemoteObservationState CreateState(
        RemoteRuntimeHostId runtimeHostId)
    {
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId("controller-01"),
                "Controller",
                new InstrumentKind("Controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            new EventDescriptor(
                                DescriptorPath.Parse(
                                    "Controller.ButtonPressed"),
                                "Button Pressed")
                        ])
            };
        var endpoint =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "7f88a60b-ff77-420f-bc7d-73ad82c718e9")),
                new EndpointDescriptor(
                    new EndpointId("arduino-uno-01"),
                    [instrument]),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready));
        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    runtimeHostId,
                    RuntimeHostClientApiVersion.Current,
                    [endpoint]),
                new RemoteObservationSequence(0)));
    }

    private static RemoteRuntimeHostObservation CreateEventObservation(
        RemoteObservationState state)
    {
        RemoteEndpointAttachmentSnapshot endpoint =
            Assert.Single(
                state.Snapshot!.Attachments);
        return new RemoteRuntimeHostObservation(
            new RemoteObservationSequence(1),
            endpoint.Key,
            new RemoteEventOccurredObservationPayload(
                new InstrumentId("controller-01"),
                DescriptorPath.Parse(
                    "Controller.ButtonPressed"),
                DateTimeOffset.UnixEpoch,
                null));
    }
}
