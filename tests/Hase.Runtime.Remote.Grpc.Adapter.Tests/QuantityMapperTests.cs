using Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class QuantityMapperTests
{
    [Fact]
    public void Map_NullQuantity_ShouldThrow()
    {
        var mapper =
            new QuantityMapper();

        Assert.Throws<ArgumentNullException>(
            "quantity",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_Quantity_ShouldPreserveIdentityAndDisplayName()
    {
        var mapper =
            new QuantityMapper();

        GrpcV1.Quantity result =
            mapper.Map(
                new Quantity(
                    "temperature",
                    "Temperature"));

        Assert.Equal(
            "temperature",
            result.Id);
        Assert.Equal(
            "Temperature",
            result.DisplayName);
    }
}
