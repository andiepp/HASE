using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteCommandOperationStatusTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RemoteCommandOperationStatus.Unspecified);
        Assert.Equal(
            1,
            (int) RemoteCommandOperationStatus.Success);
        Assert.Equal(
            2,
            (int) RemoteCommandOperationStatus.AttachmentNotCurrent);
        Assert.Equal(
            3,
            (int) RemoteCommandOperationStatus.InstrumentNotFound);
        Assert.Equal(
            4,
            (int) RemoteCommandOperationStatus.CommandNotFound);
        Assert.Equal(
            5,
            (int) RemoteCommandOperationStatus.ArgumentNotSupported);
        Assert.Equal(
            6,
            (int) RemoteCommandOperationStatus.EndpointUnavailable);
        Assert.Equal(
            7,
            (int) RemoteCommandOperationStatus.EndpointRejected);
        Assert.Equal(
            8,
            (int) RemoteCommandOperationStatus.EndpointFailure);
        Assert.Equal(
            9,
            (int) RemoteCommandOperationStatus.TimedOut);
    }
}
