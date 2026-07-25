using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class CommandDescriptorMapperTests
{
    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        var mapper =
            new CommandDescriptorMapper();

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
            new CommandDescriptorMapper();

        GrpcV1.CommandDescriptor result =
            mapper.Map(
                new CommandDescriptor(
                    new DescriptorPath(
                        "Acquisition",
                        "Start"),
                    "Start acquisition"));

        Assert.Equal(
            new[]
            {
                "Acquisition",
                "Start"
            },
            result.PathSegments.ToArray());
        Assert.Equal(
            "Start acquisition",
            result.DisplayName);
        Assert.False(
            result.HasDescription);
    }

    [Fact]
    public void Map_Description_ShouldPreserveOptionalValue()
    {
        var mapper =
            new CommandDescriptorMapper();

        GrpcV1.CommandDescriptor result =
            mapper.Map(
                new CommandDescriptor(
                    new DescriptorPath(
                        "Acquisition",
                        "Stop"),
                    "Stop acquisition")
                {
                    Description =
                        "Stops the active acquisition."
                });

        Assert.True(
            result.HasDescription);
        Assert.Equal(
            "Stops the active acquisition.",
            result.Description);
    }
}
