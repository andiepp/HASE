using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class CommandDescriptorMapperTests
{
    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        var mapper =
            new CommandDescriptorMapper(
                new TestDataDescriptorMapper());

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Constructor_NullDataDescriptorMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "dataDescriptorMapper",
            () =>
                new CommandDescriptorMapper(
                    null!));
    }

    [Fact]
    public void Map_RequiredMembers_ShouldPreservePathOrderAndLeaveDescriptionAbsent()
    {
        var mapper =
            new CommandDescriptorMapper(
                new TestDataDescriptorMapper());

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
        Assert.Null(
            result.Argument);
    }

    [Fact]
    public void Map_Description_ShouldPreserveOptionalValue()
    {
        var mapper =
            new CommandDescriptorMapper(
                new TestDataDescriptorMapper());

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

    [Fact]
    public void Map_Argument_ShouldPreserveMetadataAndDelegateDataDescriptor()
    {
        var sourceData =
            new ByteArrayDataDescriptor();
        var mappedData =
            new GrpcV1.DataDescriptor
            {
                ByteArrayDescriptor =
                    new GrpcV1.ByteArrayDataDescriptor()
            };
        var dataMapper =
            new TestDataDescriptorMapper(
                mappedData);

        var mapper =
            new CommandDescriptorMapper(
                dataMapper);

        GrpcV1.CommandDescriptor result =
            mapper.Map(
                new CommandDescriptor(
                    new DescriptorPath(
                        "Transfer",
                        "Send"),
                    "Send bytes",
                    new CommandArgumentDescriptor(
                        "Payload",
                        sourceData)
                    {
                        Description =
                            "Opaque payload bytes."
                    }));

        Assert.NotNull(
            result.Argument);
        Assert.Equal(
            "Payload",
            result.Argument.DisplayName);
        Assert.True(
            result.Argument.HasDescription);
        Assert.Equal(
            "Opaque payload bytes.",
            result.Argument.Description);
        Assert.Same(
            sourceData,
            dataMapper.Input);
        Assert.Same(
            mappedData,
            result.Argument.Data);
    }

    [Fact]
    public void Map_ArgumentDataMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new CommandDescriptorMapper(
                new TestDataDescriptorMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        new CommandDescriptor(
                            new DescriptorPath(
                                "Transfer",
                                "Send"),
                            "Send bytes",
                            new CommandArgumentDescriptor(
                                "Payload",
                                new ByteArrayDataDescriptor()))));

        Assert.Equal(
            "The data descriptor mapper returned null.",
            exception.Message);
    }

    private sealed class TestDataDescriptorMapper
        : IDataDescriptorMapper
    {
        private readonly GrpcV1.DataDescriptor result;

        public TestDataDescriptorMapper()
            : this(
                new GrpcV1.DataDescriptor())
        {
        }

        public TestDataDescriptorMapper(
            GrpcV1.DataDescriptor result)
        {
            this.result =
                result;
        }

        public DataDescriptor? Input
        {
            get;
            private set;
        }

        public GrpcV1.DataDescriptor Map(
            DataDescriptor descriptor)
        {
            Input =
                descriptor;

            return result;
        }
    }
}
