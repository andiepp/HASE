using Hase.Client.Configuration;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests.Configuration;

public sealed class RuntimeHostProfileEventOccurredEventArgsTests
{
    [Fact]
    public void Constructor_ShouldPreserveQualifiedObservation()
    {
        RuntimeHostProfileId profileId = new("first");
        RemoteRuntimeHostId hostId = new("host-01");
        RemoteRuntimeHostObservation observation = CreateEvent();
        var args = new RuntimeHostProfileEventOccurredEventArgs(profileId, hostId, observation);
        Assert.Same(profileId, args.ProfileId);
        Assert.Same(hostId, args.RuntimeHostId);
        Assert.Same(observation, args.Observation);
    }

    [Fact]
    public void Constructor_NonEventObservation_ShouldThrow()
    {
        var observation = new RemoteRuntimeHostObservation(
            new RemoteObservationSequence(1),
            CreateAttachment(),
            new RemoteAttachmentEndedObservationPayload(DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>("observation", () => new RuntimeHostProfileEventOccurredEventArgs(
            new RuntimeHostProfileId("first"), new RemoteRuntimeHostId("host-01"), observation));
    }

    [Fact]
    public void Constructor_NullValues_ShouldThrow()
    {
        RemoteRuntimeHostObservation observation = CreateEvent();
        Assert.Throws<ArgumentNullException>("profileId", () => new RuntimeHostProfileEventOccurredEventArgs(null!, new RemoteRuntimeHostId("host-01"), observation));
        Assert.Throws<ArgumentNullException>("runtimeHostId", () => new RuntimeHostProfileEventOccurredEventArgs(new RuntimeHostProfileId("first"), null!, observation));
        Assert.Throws<ArgumentNullException>("observation", () => new RuntimeHostProfileEventOccurredEventArgs(new RuntimeHostProfileId("first"), new RemoteRuntimeHostId("host-01"), null!));
    }

    private static RemoteRuntimeHostObservation CreateEvent() =>
        new(new RemoteObservationSequence(1), CreateAttachment(),
            new RemoteEventOccurredObservationPayload(
                new InstrumentId("controller-01"),
                new DescriptorPath("Controller", "ButtonPressed"),
                DateTimeOffset.UnixEpoch,
                null));
    private static RemoteEndpointAttachmentKey CreateAttachment() =>
        new(new EndpointId("endpoint-01"), new RemoteEndpointAttachmentGeneration(Guid.Parse("0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")));
}
