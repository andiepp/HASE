using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol;

/// <summary>
/// Encodes and decodes the binary payloads of HASE protocol messages.
/// </summary>
public sealed class BinaryProtocolPayloadCodec
    : IProtocolPayloadCodec
{
    /// <inheritdoc />
    public ProtocolEnvelope Encode(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            DiscoverRequest request =>
                EncodeDiscoverRequest(request),

            DiscoverResponse response =>
                EncodeDiscoverResponse(response),

            ReadEndpointDescriptorRequest request =>
                EncodeReadEndpointDescriptorRequest(request),

            ReadEndpointDescriptorResponse response =>
                EncodeReadEndpointDescriptorResponse(response),

            ReadPropertyRequest request =>
                EncodeReadPropertyRequest(request),

            ReadPropertyResponse response =>
                EncodeReadPropertyResponse(response),

            WritePropertyRequest request =>
                EncodeWritePropertyRequest(request),

            WritePropertyResponse response =>
                EncodeWritePropertyResponse(response),

            ExecuteCommandRequest request =>
                EncodeExecuteCommandRequest(request),

            ExecuteCommandResponse response =>
                EncodeExecuteCommandResponse(response),

            EventNotification notification =>
                EncodeEventNotification(notification),

            _ => throw new NotSupportedException(
                $"Encoding message type '{message.MessageType}' " +
                "is not supported.")
        };
    }

    /// <inheritdoc />
    public ProtocolMessage Decode(
        ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        ValidateVersion(envelope.Version);

        return envelope.MessageType switch
        {
            ProtocolMessageType.DiscoverRequest =>
                DecodeDiscoverRequest(envelope),

            ProtocolMessageType.DiscoverResponse =>
                DecodeDiscoverResponse(envelope),

            ProtocolMessageType.ReadEndpointDescriptorRequest =>
                DecodeReadEndpointDescriptorRequest(envelope),

            ProtocolMessageType.ReadEndpointDescriptorResponse =>
                DecodeReadEndpointDescriptorResponse(envelope),

            ProtocolMessageType.ReadPropertyRequest =>
                DecodeReadPropertyRequest(envelope),

            ProtocolMessageType.ReadPropertyResponse =>
                DecodeReadPropertyResponse(envelope),

            ProtocolMessageType.WritePropertyRequest =>
                DecodeWritePropertyRequest(envelope),

            ProtocolMessageType.WritePropertyResponse =>
                DecodeWritePropertyResponse(envelope),

            ProtocolMessageType.ExecuteCommandRequest =>
                DecodeExecuteCommandRequest(envelope),

            ProtocolMessageType.ExecuteCommandResponse =>
                DecodeExecuteCommandResponse(envelope),

            ProtocolMessageType.EventNotification =>
                DecodeEventNotification(envelope),

            _ => throw new NotSupportedException(
                $"Decoding message type '{envelope.MessageType}' " +
                "is not supported.")
        };
    }

    private static ProtocolEnvelope EncodeDiscoverRequest(
        DiscoverRequest request)
    {
        return new ProtocolEnvelope(
            request.Version,
            request.Role,
            request.MessageType,
            request.CorrelationId,
            ReadOnlyMemory<byte>.Empty);
    }

    private static DiscoverRequest DecodeDiscoverRequest(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Request);

        if (!envelope.Payload.IsEmpty)
        {
            throw new InvalidDataException(
                "A DiscoverRequest envelope must have an empty payload.");
        }

        return new DiscoverRequest(
            envelope.CorrelationId);
    }

    private static ProtocolEnvelope EncodeDiscoverResponse(
        DiscoverResponse response)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(
            response.EndpointId.Value);

        writer.WriteCount(
            response.InstrumentIds.Count);

        foreach (InstrumentId instrumentId in response.InstrumentIds)
        {
            writer.WriteString(
                instrumentId.Value);
        }

        return new ProtocolEnvelope(
            response.Version,
            response.Role,
            response.MessageType,
            response.CorrelationId,
            writer.ToArray());
    }

    private static DiscoverResponse DecodeDiscoverResponse(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Response);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        EndpointId endpointId =
            new(reader.ReadString());

        int instrumentCount =
            reader.ReadCount();

        List<InstrumentId> instrumentIds =
            new(instrumentCount);

        for (int index = 0; index < instrumentCount; index++)
        {
            instrumentIds.Add(
                new InstrumentId(
                    reader.ReadString()));
        }

        reader.EnsureFullyConsumed();

        return new DiscoverResponse(
            envelope.CorrelationId,
            endpointId,
            instrumentIds);
    }

    private static ProtocolEnvelope EncodeReadEndpointDescriptorRequest(
        ReadEndpointDescriptorRequest request)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(
            request.EndpointId.Value);

        return new ProtocolEnvelope(
            request.Version,
            request.Role,
            request.MessageType,
            request.CorrelationId,
            writer.ToArray());
    }

    private static ReadEndpointDescriptorRequest
        DecodeReadEndpointDescriptorRequest(
            ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Request);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        EndpointId endpointId =
            new(reader.ReadString());

        reader.EnsureFullyConsumed();

        return new ReadEndpointDescriptorRequest(
            envelope.CorrelationId,
            endpointId);
    }

    private static ProtocolEnvelope EncodeReadEndpointDescriptorResponse(
    ReadEndpointDescriptorResponse response)
    {
        BinaryProtocolWriter writer = new();

        WriteProtocolResult(
            writer,
            response.Result);

        if (response.Descriptor is null)
        {
            writer.WriteByte(0);
        }
        else
        {
            writer.WriteByte(1);

            EndpointDescriptorSerializer serializer = new();

            serializer.Write(
                writer,
                response.Descriptor);
        }

        return new ProtocolEnvelope(
            response.Version,
            response.Role,
            response.MessageType,
            response.CorrelationId,
            writer.ToArray());
    }

    private static ReadEndpointDescriptorResponse
        DecodeReadEndpointDescriptorResponse(
            ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Response);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        ProtocolResult result =
            ReadProtocolResult(reader);

        byte descriptorMarker =
            reader.ReadByte();

        Hase.Core.Domain.Endpoints.EndpointDescriptor? descriptor =
            descriptorMarker switch
            {
                0 => null,

                1 => new EndpointDescriptorSerializer()
                    .Read(reader),

                _ => throw new InvalidDataException(
                    $"Invalid endpoint descriptor marker " +
                    $"'{descriptorMarker}'.")
            };

        reader.EnsureFullyConsumed();

        return new ReadEndpointDescriptorResponse(
            envelope.CorrelationId,
            result,
            descriptor);
    }

    private static void WriteProtocolResult(
        BinaryProtocolWriter writer,
        ProtocolResult result)
    {
        writer.WriteByte(
            (byte)result.Code);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            result.Message);
    }

    private static ProtocolResult ReadProtocolResult(
        BinaryProtocolReader reader)
    {
        byte encodedCode =
            reader.ReadByte();

        ProtocolResultCode code =
            (ProtocolResultCode)encodedCode;

        if (!Enum.IsDefined(code))
        {
            throw new InvalidDataException(
                $"Unknown protocol result code '{encodedCode}'.");
        }

        string? message =
            ProtocolSerializationHelper.ReadOptionalString(
                reader);

        return new ProtocolResult(
            code,
            message);
    }

    private static ProtocolEnvelope EncodeReadPropertyRequest(
    ReadPropertyRequest request)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(
            request.InstrumentId.Value);

        writer.WriteString(
            request.PropertyId.Value);

        return new ProtocolEnvelope(
            request.Version,
            request.Role,
            request.MessageType,
            request.CorrelationId,
            writer.ToArray());
    }

    private static ReadPropertyRequest DecodeReadPropertyRequest(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Request);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        InstrumentId instrumentId =
            new(reader.ReadString());

        PropertyId propertyId =
            new(reader.ReadString());

        reader.EnsureFullyConsumed();

        return new ReadPropertyRequest(
            envelope.CorrelationId,
            instrumentId,
            propertyId);
    }

    private static ProtocolEnvelope EncodeReadPropertyResponse(
        ReadPropertyResponse response)
    {
        BinaryProtocolWriter writer = new();

        WriteProtocolResult(
            writer,
            response.Result);

        if (response.PropertyValue is null)
        {
            writer.WriteByte(0);
        }
        else
        {
            writer.WriteByte(1);

            PropertyValueSerializer serializer = new();

            serializer.Write(
                writer,
                response.PropertyValue);
        }

        return new ProtocolEnvelope(
            response.Version,
            response.Role,
            response.MessageType,
            response.CorrelationId,
            writer.ToArray());
    }

    private static ReadPropertyResponse DecodeReadPropertyResponse(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Response);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        ProtocolResult result =
            ReadProtocolResult(reader);

        byte propertyValueMarker =
            reader.ReadByte();

        Hase.Core.Domain.Properties.PropertyValue? propertyValue =
            propertyValueMarker switch
            {
                0 => null,

                1 => new PropertyValueSerializer()
                    .Read(reader),

                _ => throw new InvalidDataException(
                    $"Invalid property-value marker " +
                    $"'{propertyValueMarker}'.")
            };

        reader.EnsureFullyConsumed();

        return new ReadPropertyResponse(
            envelope.CorrelationId,
            result,
            propertyValue);
    }

    private static ProtocolEnvelope EncodeWritePropertyRequest(
    WritePropertyRequest request)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(
            request.InstrumentId.Value);

        writer.WriteString(
            request.PropertyId.Value);

        new VariantSerializer().Write(
            writer,
            request.Value);

        return new ProtocolEnvelope(
            request.Version,
            request.Role,
            request.MessageType,
            request.CorrelationId,
            writer.ToArray());
    }

    private static WritePropertyRequest DecodeWritePropertyRequest(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Request);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        InstrumentId instrumentId =
            new(reader.ReadString());

        PropertyId propertyId =
            new(reader.ReadString());

        object? value =
            new VariantSerializer().Read(reader);

        reader.EnsureFullyConsumed();

        return new WritePropertyRequest(
            envelope.CorrelationId,
            instrumentId,
            propertyId,
            value);
    }

    private static ProtocolEnvelope EncodeWritePropertyResponse(
        WritePropertyResponse response)
    {
        BinaryProtocolWriter writer = new();

        WriteProtocolResult(
            writer,
            response.Result);

        if (response.PropertyValue is null)
        {
            writer.WriteByte(0);
        }
        else
        {
            writer.WriteByte(1);

            new PropertyValueSerializer().Write(
                writer,
                response.PropertyValue);
        }

        return new ProtocolEnvelope(
            response.Version,
            response.Role,
            response.MessageType,
            response.CorrelationId,
            writer.ToArray());
    }

    private static WritePropertyResponse DecodeWritePropertyResponse(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Response);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        ProtocolResult result =
            ReadProtocolResult(reader);

        byte marker =
            reader.ReadByte();

        PropertyValue? propertyValue =
            marker switch
            {
                0 => null,

                1 => new PropertyValueSerializer()
                        .Read(reader),

                _ => throw new InvalidDataException(
                    $"Invalid property-value marker '{marker}'.")
            };

        reader.EnsureFullyConsumed();

        return new WritePropertyResponse(
            envelope.CorrelationId,
            result,
            propertyValue);
    }

    private static ProtocolEnvelope EncodeExecuteCommandRequest(
    ExecuteCommandRequest request)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(
            request.InstrumentId.Value);

        writer.WriteString(
            request.CommandPath.ToString());

        new VariantSerializer().Write(
            writer,
            request.Argument);

        return new ProtocolEnvelope(
            request.Version,
            request.Role,
            request.MessageType,
            request.CorrelationId,
            writer.ToArray());
    }

    private static ExecuteCommandRequest DecodeExecuteCommandRequest(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Request);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        InstrumentId instrumentId =
            new(reader.ReadString());

        Hase.Core.Domain.Properties.DescriptorPath commandPath =
            Hase.Core.Domain.Properties.DescriptorPath.Parse(
                reader.ReadString());

        object? argument =
            new VariantSerializer().Read(reader);

        reader.EnsureFullyConsumed();

        return new ExecuteCommandRequest(
            envelope.CorrelationId,
            instrumentId,
            commandPath,
            argument);
    }

    private static ProtocolEnvelope EncodeExecuteCommandResponse(
        ExecuteCommandResponse response)
    {
        BinaryProtocolWriter writer = new();

        WriteProtocolResult(
            writer,
            response.Result);

        new VariantSerializer().Write(
            writer,
            response.ReturnValue);

        return new ProtocolEnvelope(
            response.Version,
            response.Role,
            response.MessageType,
            response.CorrelationId,
            writer.ToArray());
    }

    private static ExecuteCommandResponse DecodeExecuteCommandResponse(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Response);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        ProtocolResult result =
            ReadProtocolResult(reader);

        object? returnValue =
            new VariantSerializer().Read(reader);

        reader.EnsureFullyConsumed();

        return new ExecuteCommandResponse(
            envelope.CorrelationId,
            result,
            returnValue);
    }

    private static ProtocolEnvelope EncodeEventNotification(
    EventNotification notification)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(
            notification.InstrumentId.Value);

        writer.WriteString(
            notification.EventPath.ToString());

        WriteUnixTimeMilliseconds(
            writer,
            notification.TimestampUtc.ToUnixTimeMilliseconds());

        new VariantSerializer().Write(
            writer,
            notification.Value);

        return new ProtocolEnvelope(
            notification.Version,
            notification.Role,
            notification.MessageType,
            notification.CorrelationId,
            writer.ToArray());
    }

    private static EventNotification DecodeEventNotification(
        ProtocolEnvelope envelope)
    {
        ValidateRole(
            envelope,
            ProtocolMessageRole.Notification);

        BinaryProtocolReader reader =
            new(envelope.Payload);

        InstrumentId instrumentId =
            new(reader.ReadString());

        DescriptorPath eventPath =
            DescriptorPath.Parse(
                reader.ReadString());

        long unixTimeMilliseconds =
            ReadUnixTimeMilliseconds(reader);

        DateTimeOffset timestampUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                unixTimeMilliseconds);

        object? value =
            new VariantSerializer().Read(reader);

        reader.EnsureFullyConsumed();

        return new EventNotification(
            instrumentId,
            eventPath,
            timestampUtc,
            value);
    }

    private static void WriteUnixTimeMilliseconds(
        BinaryProtocolWriter writer,
        long value)
    {
        Span<byte> bytes = stackalloc byte[8];

        System.Buffers.Binary.BinaryPrimitives
            .WriteInt64LittleEndian(bytes, value);

        foreach (byte b in bytes)
        {
            writer.WriteByte(b);
        }
    }

    private static long ReadUnixTimeMilliseconds(
        BinaryProtocolReader reader)
    {
        Span<byte> bytes = stackalloc byte[8];

        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = reader.ReadByte();
        }

        return System.Buffers.Binary.BinaryPrimitives
            .ReadInt64LittleEndian(bytes);
    }

    private static void ValidateVersion(
        ProtocolVersion version)
    {
        if (version != ProtocolVersion.Current)
        {
            throw new InvalidDataException(
                $"Protocol version '{version}' is not supported. " +
                $"Expected version '{ProtocolVersion.Current}'.");
        }
    }

    private static void ValidateRole(
        ProtocolEnvelope envelope,
        ProtocolMessageRole expectedRole)
    {
        if (envelope.Role != expectedRole)
        {
            throw new InvalidDataException(
                $"Message type '{envelope.MessageType}' must have " +
                $"the '{expectedRole}' role.");
        }
    }
}