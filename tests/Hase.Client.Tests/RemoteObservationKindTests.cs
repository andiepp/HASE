using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteObservationKindTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RemoteObservationKind.Unspecified);
        Assert.Equal(
            1,
            (int) RemoteObservationKind.AttachmentPublished);
        Assert.Equal(
            2,
            (int) RemoteObservationKind.AttachmentEnded);
        Assert.Equal(
            3,
            (int) RemoteObservationKind.ConnectionStatusChanged);
        Assert.Equal(
            4,
            (int) RemoteObservationKind.PropertyValueChanged);
        Assert.Equal(
            5,
            (int) RemoteObservationKind.EventOccurred);
    }
}
