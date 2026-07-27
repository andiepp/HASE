using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemotePropertyOperationStatusTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RemotePropertyOperationStatus.Unspecified);
        Assert.Equal(
            1,
            (int) RemotePropertyOperationStatus.Success);
        Assert.Equal(
            2,
            (int) RemotePropertyOperationStatus.AttachmentNotCurrent);
        Assert.Equal(
            3,
            (int) RemotePropertyOperationStatus.InstrumentNotFound);
        Assert.Equal(
            4,
            (int) RemotePropertyOperationStatus.PropertyNotFound);
        Assert.Equal(
            5,
            (int) RemotePropertyOperationStatus.ReadNotSupported);
        Assert.Equal(
            6,
            (int) RemotePropertyOperationStatus.WriteNotSupported);
        Assert.Equal(
            7,
            (int) RemotePropertyOperationStatus.InvalidValue);
        Assert.Equal(
            8,
            (int) RemotePropertyOperationStatus.EndpointUnavailable);
        Assert.Equal(
            9,
            (int) RemotePropertyOperationStatus.EndpointRejected);
        Assert.Equal(
            10,
            (int) RemotePropertyOperationStatus.EndpointFailure);
        Assert.Equal(
            11,
            (int) RemotePropertyOperationStatus.TimedOut);
    }
}
