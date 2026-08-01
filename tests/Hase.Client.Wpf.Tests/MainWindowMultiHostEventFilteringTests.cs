using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowMultiHostEventFilteringTests
{
    [Fact]
    public void UnselectedHostEvent_ShouldBeIgnored()
    {
        RuntimeHostProfile first = new(new RuntimeHostProfileId("first"), "First", new RemoteRuntimeHostId("host-01"));
        RuntimeHostProfile second = new(new RuntimeHostProfileId("second"), "Second", new RemoteRuntimeHostId("host-02"));
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([first, second]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Disconnected(first), Disconnected(second)]));
        viewModel.SelectRuntimeHost(first.ProfileId);

        viewModel.ApplyMultiHostEventOccurred(new RuntimeHostProfileEventOccurredEventArgs(
            second.ProfileId,
            second.ExpectedRuntimeHostId,
            new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(1),
                new RemoteEndpointAttachmentKey(new EndpointId("endpoint-01"), new RemoteEndpointAttachmentGeneration(Guid.NewGuid())),
                new RemoteEventOccurredObservationPayload(new InstrumentId("controller-01"), new DescriptorPath("Controller", "ButtonPressed"), DateTimeOffset.UnixEpoch, null))));

        Assert.Empty(viewModel.EventOccurrences);
    }

    private static RuntimeHostProfileSessionSnapshot Disconnected(RuntimeHostProfile profile) =>
        new(profile, new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected), DateTimeOffset.UtcNow);
}
