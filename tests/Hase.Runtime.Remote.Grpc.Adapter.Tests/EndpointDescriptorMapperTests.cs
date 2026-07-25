using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class EndpointDescriptorMapperTests
{
    [Fact]
    public void Constructor_NullInstrumentDescriptorMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "instrumentDescriptorMapper",
            () =>
                new EndpointDescriptorMapper(
                    null!));
    }

    [Fact]
    public void Map_NullDescriptor_ShouldThrow()
    {
        var mapper =
            new EndpointDescriptorMapper(
                new TestInstrumentDescriptorMapper());

        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_DescriptorWithoutMetadata_ShouldLeaveOptionalsAbsent()
    {
        var instrumentMapper =
            new TestInstrumentDescriptorMapper();

        var mapper =
            new EndpointDescriptorMapper(
                instrumentMapper);

        GrpcV1.EndpointDescriptor result =
            mapper.Map(
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-1")));

        Assert.Equal(
            "endpoint-1",
            result.EndpointId);
        Assert.False(
            result.HasDisplayName);
        Assert.False(
            result.HasDescription);
        Assert.Empty(
            result.Instruments);
        Assert.Empty(
            instrumentMapper.Inputs);
    }

    [Fact]
    public void Map_OptionalMetadata_ShouldPreserveValues()
    {
        var mapper =
            new EndpointDescriptorMapper(
                new TestInstrumentDescriptorMapper());

        GrpcV1.EndpointDescriptor result =
            mapper.Map(
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-1"))
                {
                    Metadata =
                        new EndpointMetadata
                        {
                            DisplayName =
                                "Endpoint 1",
                            Description =
                                "Validation endpoint."
                        }
                });

        Assert.True(
            result.HasDisplayName);
        Assert.Equal(
            "Endpoint 1",
            result.DisplayName);
        Assert.True(
            result.HasDescription);
        Assert.Equal(
            "Validation endpoint.",
            result.Description);
    }

    [Fact]
    public void Map_Instruments_ShouldDelegateInOrderAndPreserveResults()
    {
        InstrumentDescriptor firstInput =
            CreateInstrument(
                "instrument-1");
        InstrumentDescriptor secondInput =
            CreateInstrument(
                "instrument-2");

        var firstOutput =
            new GrpcV1.InstrumentDescriptor
            {
                InstrumentId =
                    "mapped-instrument-1"
            };
        var secondOutput =
            new GrpcV1.InstrumentDescriptor
            {
                InstrumentId =
                    "mapped-instrument-2"
            };

        var instrumentMapper =
            new TestInstrumentDescriptorMapper(
                firstOutput,
                secondOutput);

        var mapper =
            new EndpointDescriptorMapper(
                instrumentMapper);

        GrpcV1.EndpointDescriptor result =
            mapper.Map(
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-1"),
                    new[]
                    {
                        firstInput,
                        secondInput
                    }));

        Assert.Equal(
            new[]
            {
                firstInput,
                secondInput
            },
            instrumentMapper.Inputs.ToArray());
        Assert.Equal(
            new[]
            {
                firstOutput,
                secondOutput
            },
            result.Instruments.ToArray());
    }

    [Fact]
    public void Map_InstrumentMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new EndpointDescriptorMapper(
                new TestInstrumentDescriptorMapper(
                    new GrpcV1.InstrumentDescriptor[]
                    {
                        null!
                    }));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        new EndpointDescriptor(
                            new EndpointId(
                                "endpoint-1"),
                            new[]
                            {
                                CreateInstrument(
                                    "instrument-1")
                            })));

        Assert.Equal(
            "The instrument descriptor mapper returned null.",
            exception.Message);
    }

    private static InstrumentDescriptor CreateInstrument(
        string instrumentId)
    {
        return new InstrumentDescriptor(
            new InstrumentId(
                instrumentId),
            "Instrument",
            new InstrumentKind(
                "validation"));
    }

    private sealed class TestInstrumentDescriptorMapper
        : IInstrumentDescriptorMapper
    {
        private readonly Queue<GrpcV1.InstrumentDescriptor> results;

        public TestInstrumentDescriptorMapper(
            params GrpcV1.InstrumentDescriptor[] results)
        {
            this.results =
                new Queue<GrpcV1.InstrumentDescriptor>(
                    results);
        }

        public List<InstrumentDescriptor> Inputs
        {
            get;
        } =
            new();

        public GrpcV1.InstrumentDescriptor Map(
            InstrumentDescriptor descriptor)
        {
            Inputs.Add(
                descriptor);

            return results.Dequeue();
        }
    }
}
