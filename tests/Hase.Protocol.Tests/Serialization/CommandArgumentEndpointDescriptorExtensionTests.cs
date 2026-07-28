using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class CommandArgumentEndpointDescriptorExtensionTests
{
    [Fact]
    public void Encode_ParameterlessEndpoint_PreservesLegacyPayloadBytes()
    {
        EndpointDescriptor descriptor =
            CreateEndpoint(
                "controller",
                CreateParameterlessCommand(
                    "Controller.Reset"));

        ReadEndpointDescriptorResponse response =
            CreateResponse(
                descriptor);

        BinaryProtocolPayloadCodec codec =
            new();

        ProtocolEnvelope encoded =
            codec.Encode(
                response);

        BinaryProtocolWriter legacyWriter =
            new();

        legacyWriter.WriteByte(
            (byte)ProtocolResultCode.Success);

        ProtocolSerializationHelper.WriteOptionalString(
            legacyWriter,
            null);

        legacyWriter.WriteByte(
            0x01);

        new EndpointDescriptorSerializer().Write(
            legacyWriter,
            descriptor);

        Assert.Equal(
            legacyWriter.ToArray(),
            encoded.Payload.ToArray());
    }

    [Fact]
    public void RoundTrip_BooleanCommandArgument_PreservesTypedDescriptor()
    {
        CommandArgumentDescriptor argument =
            new(
                "Requested State",
                new BooleanDataDescriptor())
            {
                Description =
                    "The requested controller state."
            };

        CommandDescriptor decoded =
            RoundTripTypedCommand(
                argument);

        Assert.Equal(
            argument,
            decoded.Argument);
    }

    [Fact]
    public void RoundTrip_ByteArrayCommandArgument_PreservesTypedDescriptor()
    {
        CommandArgumentDescriptor argument =
            new(
                "Payload",
                new ByteArrayDataDescriptor())
            {
                Description =
                    "Opaque application-defined bytes."
            };

        CommandDescriptor decoded =
            RoundTripTypedCommand(
                argument);

        Assert.Equal(
            argument,
            decoded.Argument);

        Assert.IsType<ByteArrayDataDescriptor>(
            decoded.Argument!.Data);
    }

    [Fact]
    public void ApplyExtensions_UnknownExtensionType_IsSkipped()
    {
        EndpointDescriptor descriptor =
            CreateEndpoint(
                "controller",
                CreateParameterlessCommand(
                    "Controller.Reset"));

        EndpointDescriptor decoded =
            new CommandArgumentEndpointDescriptorExtensionMapper()
                .ApplyExtensions(
                    descriptor,
                    new[]
                    {
                        new EndpointDescriptorExtension(
                            0xFE,
                            new byte[]
                            {
                                0x01
                            })
                    });

        Assert.Same(
            descriptor,
            decoded);
    }

    [Fact]
    public void ApplyExtensions_DuplicateCommandTarget_ThrowsInvalidDataException()
    {
        EndpointDescriptor typed =
            CreateTypedEndpoint(
                "controller",
                "Controller.Send");

        CommandArgumentEndpointDescriptorExtensionMapper mapper =
            new();

        EndpointDescriptorExtension extension =
            Assert.Single(
                mapper.CreateExtensions(
                    typed));

        EndpointDescriptor parameterless =
            CreateEndpoint(
                "controller",
                CreateParameterlessCommand(
                    "Controller.Send"));

        Assert.Throws<InvalidDataException>(
            () => mapper.ApplyExtensions(
                parameterless,
                new[]
                {
                    extension,
                    extension
                }));
    }

    [Fact]
    public void ApplyExtensions_MissingInstrument_ThrowsInvalidDataException()
    {
        CommandArgumentEndpointDescriptorExtensionMapper mapper =
            new();

        EndpointDescriptorExtension extension =
            Assert.Single(
                mapper.CreateExtensions(
                    CreateTypedEndpoint(
                        "missing-controller",
                        "Controller.Send")));

        EndpointDescriptor parameterless =
            CreateEndpoint(
                "controller",
                CreateParameterlessCommand(
                    "Controller.Send"));

        Assert.Throws<InvalidDataException>(
            () => mapper.ApplyExtensions(
                parameterless,
                new[]
                {
                    extension
                }));
    }

    [Fact]
    public void ApplyExtensions_MissingCommand_ThrowsInvalidDataException()
    {
        CommandArgumentEndpointDescriptorExtensionMapper mapper =
            new();

        EndpointDescriptorExtension extension =
            Assert.Single(
                mapper.CreateExtensions(
                    CreateTypedEndpoint(
                        "controller",
                        "Controller.Send")));

        EndpointDescriptor parameterless =
            CreateEndpoint(
                "controller",
                CreateParameterlessCommand(
                    "Controller.Reset"));

        Assert.Throws<InvalidDataException>(
            () => mapper.ApplyExtensions(
                parameterless,
                new[]
                {
                    extension
                }));
    }

    [Fact]
    public void ApplyExtensions_TrailingPayloadByte_ThrowsInvalidDataException()
    {
        CommandArgumentEndpointDescriptorExtensionMapper mapper =
            new();

        EndpointDescriptorExtension valid =
            Assert.Single(
                mapper.CreateExtensions(
                    CreateTypedEndpoint(
                        "controller",
                        "Controller.Send")));

        byte[] malformedPayload =
            valid.Payload.ToArray()
                .Concat(
                    new byte[]
                    {
                        0xFF
                    })
                .ToArray();

        EndpointDescriptorExtension malformed =
            new(
                CommandArgumentEndpointDescriptorExtensionMapper.ExtensionType,
                malformedPayload);

        EndpointDescriptor parameterless =
            CreateEndpoint(
                "controller",
                CreateParameterlessCommand(
                    "Controller.Send"));

        Assert.Throws<InvalidDataException>(
            () => mapper.ApplyExtensions(
                parameterless,
                new[]
                {
                    malformed
                }));
    }

    private static CommandDescriptor RoundTripTypedCommand(
        CommandArgumentDescriptor argument)
    {
        CommandDescriptor command =
            new(
                DescriptorPath.Parse(
                    "Controller.Send"),
                "Send",
                argument)
            {
                Description =
                    "Send one typed value."
            };

        ReadEndpointDescriptorResponse original =
            CreateResponse(
                CreateEndpoint(
                    "controller",
                    command));

        BinaryProtocolPayloadCodec codec =
            new();

        ProtocolEnvelope envelope =
            codec.Encode(
                original);

        ReadEndpointDescriptorResponse decoded =
            Assert.IsType<ReadEndpointDescriptorResponse>(
                codec.Decode(
                    envelope));

        InstrumentDescriptor instrument =
            Assert.Single(
                decoded.Descriptor!.Instruments);

        CommandDescriptor decodedCommand =
            Assert.Single(
                instrument.Interface.Commands);

        Assert.Equal(
            command.Path,
            decodedCommand.Path);

        Assert.Equal(
            command.DisplayName,
            decodedCommand.DisplayName);

        Assert.Equal(
            command.Description,
            decodedCommand.Description);

        return decodedCommand;
    }

    private static EndpointDescriptor CreateTypedEndpoint(
        string instrumentId,
        string commandPath)
    {
        return CreateEndpoint(
            instrumentId,
            new CommandDescriptor(
                DescriptorPath.Parse(
                    commandPath),
                "Send",
                new CommandArgumentDescriptor(
                    "Payload",
                    new ByteArrayDataDescriptor())));
    }

    private static EndpointDescriptor CreateEndpoint(
        string instrumentId,
        CommandDescriptor command)
    {
        InstrumentDescriptor instrument =
            new(
                new InstrumentId(
                    instrumentId),
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        Array.Empty<PropertyDescriptor>(),
                        new[]
                        {
                            command
                        },
                        Array.Empty<EventDescriptor>())
            };

        return new EndpointDescriptor(
            new EndpointId(
                "endpoint"),
            new[]
            {
                instrument
            });
    }

    private static CommandDescriptor CreateParameterlessCommand(
        string path)
    {
        return new CommandDescriptor(
            DescriptorPath.Parse(
                path),
            "Command");
    }

    private static ReadEndpointDescriptorResponse CreateResponse(
        EndpointDescriptor descriptor)
    {
        return new ReadEndpointDescriptorResponse(
            new CorrelationId(
                3606),
            ProtocolResult.Success,
            descriptor);
    }
}
