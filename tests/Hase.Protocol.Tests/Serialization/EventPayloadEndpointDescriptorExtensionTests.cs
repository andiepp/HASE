using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class EventPayloadEndpointDescriptorExtensionTests
{
    [Fact]
    public void RoundTrip_ByteArrayEventPayload_PreservesTypedDescriptor()
    {
        EventPayloadDescriptor payload =
            new(
                "Buffer value",
                new ByteArrayDataDescriptor())
            {
                Description =
                    "Replacement bytes."
            };
        EventDescriptor eventDescriptor =
            new(
                DescriptorPath.Parse(
                    "Buffer.Replaced"),
                "Buffer replaced")
            {
                Description =
                    "Raised after replacement.",
                Payload =
                    payload
            };
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope =
            codec.Encode(
                CreateResponse(
                    CreateEndpoint(
                        "buffer",
                        eventDescriptor)));
        ReadEndpointDescriptorResponse decoded =
            Assert.IsType<ReadEndpointDescriptorResponse>(
                codec.Decode(
                    envelope));
        EventDescriptor decodedEvent =
            Assert.Single(
                Assert.Single(
                    decoded.Descriptor!.Instruments)
                    .Interface.Events);

        Assert.Equal(
            eventDescriptor,
            decodedEvent);
        Assert.Equal(
            payload,
            decodedEvent.Payload);
        Assert.IsType<ByteArrayDataDescriptor>(
            decodedEvent.Payload!.Data);
    }

    [Fact]
    public void ApplyExtensions_DuplicateTarget_ShouldThrow()
    {
        EndpointDescriptor descriptor =
            CreateEndpoint(
                "controller",
                new EventDescriptor(
                    DescriptorPath.Parse(
                        "Controller.ButtonPressed"),
                    "Button pressed"));
        EventPayloadEndpointDescriptorExtensionMapper mapper = new();
        EndpointDescriptor typed =
            CreateEndpoint(
                "controller",
                new EventDescriptor(
                    DescriptorPath.Parse(
                        "Controller.ButtonPressed"),
                    "Button pressed")
                {
                    Payload =
                        new EventPayloadDescriptor(
                            "Pressed",
                            new BooleanDataDescriptor())
                });
        EndpointDescriptorExtension extension =
            Assert.Single(
                mapper.CreateExtensions(
                    typed));

        Assert.Throws<InvalidDataException>(
            () =>
                mapper.ApplyExtensions(
                    descriptor,
                    new[]
                    {
                        extension,
                        extension
                    }));
    }

    [Fact]
    public void ApplyExtensions_UnknownInstrument_ShouldThrow()
    {
        EventPayloadEndpointDescriptorExtensionMapper mapper = new();
        EndpointDescriptor target =
            CreateEndpoint(
                "known",
                new EventDescriptor(
                    DescriptorPath.Parse(
                        "Controller.ButtonPressed"),
                    "Button pressed"));
        EndpointDescriptor source =
            CreateEndpoint(
                "unknown",
                new EventDescriptor(
                    DescriptorPath.Parse(
                        "Controller.ButtonPressed"),
                    "Button pressed")
                {
                    Payload =
                        new EventPayloadDescriptor(
                            "Pressed",
                            new BooleanDataDescriptor())
                });

        Assert.Throws<InvalidDataException>(
            () =>
                mapper.ApplyExtensions(
                    target,
                    mapper.CreateExtensions(
                        source)));
    }

    private static EndpointDescriptor CreateEndpoint(
        string instrumentId,
        EventDescriptor eventDescriptor)
    {
        InstrumentDescriptor instrument =
            new(
                new InstrumentId(
                    instrumentId),
                "Instrument",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        Array.Empty<PropertyDescriptor>(),
                        Array.Empty<CommandDescriptor>(),
                        new[]
                        {
                            eventDescriptor
                        })
            };

        return new EndpointDescriptor(
            new EndpointId(
                "endpoint"),
            new[]
            {
                instrument
            });
    }

    private static ReadEndpointDescriptorResponse CreateResponse(
        EndpointDescriptor descriptor) =>
        new(
            new CorrelationId(
                3901),
            ProtocolResult.Success,
            descriptor);
}
