using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostObservationKindMapperTests
{
    [Theory]
    [InlineData(Northbound.RuntimeHostObservationKind.AttachmentPublished, 1)]
    [InlineData(Northbound.RuntimeHostObservationKind.AttachmentEnded, 2)]
    [InlineData(Northbound.RuntimeHostObservationKind.ConnectionStatusChanged, 3)]
    [InlineData(Northbound.RuntimeHostObservationKind.PropertyValueChanged, 4)]
    [InlineData(Northbound.RuntimeHostObservationKind.EventOccurred, 5)]
    public void Map_Kind_ShouldUseStableRemoteValue(
        Northbound.RuntimeHostObservationKind source,
        int expectedRemoteValue)
    {
        var mapper =
            new RuntimeHostObservationKindMapper();

        var result =
            mapper.Map(
                source);

        Assert.Equal(
            expectedRemoteValue,
            (int)result);
    }

    [Fact]
    public void Map_UnknownKind_ShouldThrow()
    {
        const Northbound.RuntimeHostObservationKind unknownKind =
            (Northbound.RuntimeHostObservationKind)99;
        var mapper =
            new RuntimeHostObservationKindMapper();

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "kind",
                () =>
                    mapper.Map(
                        unknownKind));

        Assert.Equal(
            unknownKind,
            exception.ActualValue);
    }
}
