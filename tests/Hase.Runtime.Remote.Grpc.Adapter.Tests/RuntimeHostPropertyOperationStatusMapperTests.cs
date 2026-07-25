using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostPropertyOperationStatusMapperTests
{
    [Theory]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.Success, 1)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.AttachmentNotCurrent, 2)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.InstrumentNotFound, 3)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.PropertyNotFound, 4)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.ReadNotSupported, 5)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.WriteNotSupported, 6)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.InvalidValue, 7)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.EndpointUnavailable, 8)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.EndpointRejected, 9)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.EndpointFailure, 10)]
    [InlineData(Northbound.RuntimeHostPropertyOperationStatus.TimedOut, 11)]
    public void Map_Status_ShouldUseStableRemoteValue(
        Northbound.RuntimeHostPropertyOperationStatus source,
        int expectedRemoteValue)
    {
        var mapper =
            new RuntimeHostPropertyOperationStatusMapper();

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
        const Northbound.RuntimeHostPropertyOperationStatus unknownStatus =
            (Northbound.RuntimeHostPropertyOperationStatus)99;
        var mapper =
            new RuntimeHostPropertyOperationStatusMapper();

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
