using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCommandOperationStatusMapperTests
{
    [Theory]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.Success, 1)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.AttachmentNotCurrent, 2)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.InstrumentNotFound, 3)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.CommandNotFound, 4)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.ArgumentNotSupported, 5)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.EndpointUnavailable, 6)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.EndpointRejected, 7)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.EndpointFailure, 8)]
    [InlineData(Northbound.RuntimeHostCommandOperationStatus.TimedOut, 9)]
    public void Map_Status_ShouldUseStableRemoteValue(
        Northbound.RuntimeHostCommandOperationStatus source,
        int expectedRemoteValue)
    {
        var mapper =
            new RuntimeHostCommandOperationStatusMapper();

        var result =
            mapper.Map(
                source);

        Assert.Equal(
            expectedRemoteValue,
            (int)result);
    }

    [Fact]
    public void Map_UnknownStatus_ShouldThrow()
    {
        const Northbound.RuntimeHostCommandOperationStatus unknownStatus =
            (Northbound.RuntimeHostCommandOperationStatus)99;
        var mapper =
            new RuntimeHostCommandOperationStatusMapper();

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "status",
                () =>
                    mapper.Map(
                        unknownStatus));

        Assert.Equal(
            unknownStatus,
            exception.ActualValue);
    }
}
