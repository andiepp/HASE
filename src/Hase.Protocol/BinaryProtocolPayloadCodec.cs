using Hase.Core.Domain.Identity;
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