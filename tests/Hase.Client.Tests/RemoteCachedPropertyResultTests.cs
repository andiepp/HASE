using Hase.Client;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemoteCachedPropertyResultTests
{
    [Fact]
    public void Successful_Snapshot_ShouldCreateSuccess()
    {
        RemotePublishedPropertySnapshot snapshot =
            CreateSnapshot();

        RemoteCachedPropertyResult result =
            RemoteCachedPropertyResult.Successful(
                snapshot);

        Assert.Equal(
            RemotePropertyOperationStatus.Success,
            result.Status);
        Assert.True(
            result.IsSuccess);
        Assert.Same(
            snapshot,
            result.Snapshot);
        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void Successful_NullSnapshot_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshot",
            () => RemoteCachedPropertyResult.Successful(
                null!));
    }

    [Theory]
    [InlineData(RemotePropertyOperationStatus.AttachmentNotCurrent)]
    [InlineData(RemotePropertyOperationStatus.InstrumentNotFound)]
    [InlineData(RemotePropertyOperationStatus.PropertyNotFound)]
    [InlineData(RemotePropertyOperationStatus.ReadNotSupported)]
    [InlineData(RemotePropertyOperationStatus.WriteNotSupported)]
    [InlineData(RemotePropertyOperationStatus.InvalidValue)]
    [InlineData(RemotePropertyOperationStatus.EndpointUnavailable)]
    [InlineData(RemotePropertyOperationStatus.EndpointRejected)]
    [InlineData(RemotePropertyOperationStatus.EndpointFailure)]
    [InlineData(RemotePropertyOperationStatus.TimedOut)]
    public void Failed_FailureStatus_ShouldCreateFailure(
        RemotePropertyOperationStatus status)
    {
        RemoteCachedPropertyResult result =
            RemoteCachedPropertyResult.Failed(
                status,
                "  diagnostic  ");

        Assert.Equal(
            status,
            result.Status);
        Assert.False(
            result.IsSuccess);
        Assert.Null(
            result.Snapshot);
        Assert.Equal(
            "diagnostic",
            result.Diagnostic);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Failed_MissingDiagnostic_ShouldNormalizeToNull(
        string? diagnostic)
    {
        RemoteCachedPropertyResult result =
            RemoteCachedPropertyResult.Failed(
                RemotePropertyOperationStatus.EndpointUnavailable,
                diagnostic);

        Assert.Null(
            result.Diagnostic);
    }

    [Fact]
    public void Failed_UnspecifiedStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () => RemoteCachedPropertyResult.Failed(
                RemotePropertyOperationStatus.Unspecified));
    }

    [Fact]
    public void Failed_UndefinedStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () => RemoteCachedPropertyResult.Failed(
                (RemotePropertyOperationStatus) 99));
    }

    [Fact]
    public void Failed_SuccessStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "status",
            () => RemoteCachedPropertyResult.Failed(
                RemotePropertyOperationStatus.Success));
    }

    private static RemotePublishedPropertySnapshot CreateSnapshot()
    {
        var propertyId =
            new PropertyId(
                "property-01");

        return new RemotePublishedPropertySnapshot(
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"))),
                new InstrumentId(
                    "instrument-01"),
                propertyId),
            new PropertyDescriptor(
                propertyId,
                new DescriptorPath(
                    "Property"),
                "Property",
                new BooleanDataDescriptor()),
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready),
            null);
    }
}
