using Hase.Core.Domain.Events;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class EventDescriptorMapperTests
{
    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_RequiredMembers_ShouldPreservePathOrderAndLeaveDescriptionAbsent()
    {
        var mapper =
            CreateMapper();

        GrpcV1.EventDescriptor result =
            mapper.Map(
                new EventDescriptor(
                    new DescriptorPath(
                        "Input",
                        "Pressed"),
                    "Input pressed"));

        Assert.Equal(
            new[]
            {
                "Input",
                "Pressed"
            },
            result.PathSegments.ToArray());
        Assert.Equal(
            "Input pressed",
            result.DisplayName);
        Assert.False(
            result.HasDescription);
    }

    [Fact]
    public void Map_Description_ShouldPreserveOptionalValue()
    {
        var mapper =
            CreateMapper();

        GrpcV1.EventDescriptor result =
            mapper.Map(
                new EventDescriptor(
                    new DescriptorPath(
                        "Connection",
                        "Lost"),
                    "Connection lost")
                {
                    Description =
                        "The endpoint connection was lost."
                });

        Assert.True(
            result.HasDescription);
        Assert.Equal(
            "The endpoint connection was lost.",
            result.Description);
    }

    [Fact]
    public void Map_TypedValue_ShouldPreserveMetadataAndDataDescriptor()
    {
        EventDescriptorMapper mapper =
            CreateMapper();

        GrpcV1.EventDescriptor result =
            mapper.Map(
                new EventDescriptor(
                    new DescriptorPath(
                        "Buffer",
                        "Replaced"),
                    "Buffer replaced")
                {
                    Payload =
                        new EventPayloadDescriptor(
                        "Buffer value",
                        new ByteArrayDataDescriptor())
                        {
                            Description =
                                "The replacement bytes."
                        }
                });

        Assert.NotNull(
            result.Payload);
        Assert.Equal(
            "Buffer value",
            result.Payload.DisplayName);
        Assert.Equal(
            "The replacement bytes.",
            result.Payload.Description);
        Assert.NotNull(
            result.Payload.Data.ByteArrayDescriptor);
    }

    private static EventDescriptorMapper CreateMapper()
    {
        var quantityMapper =
            new QuantityMapper();
        var unitMapper =
            new UnitMapper(
                quantityMapper);

        return new EventDescriptorMapper(
            new DataDescriptorMapper(
                new NumericDataDescriptorMapper(
                    quantityMapper,
                    unitMapper)));
    }
}
