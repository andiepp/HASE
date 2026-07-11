using Hase.Core.Domain.Identity;

namespace Hase.Protocol;

public sealed class BinaryProtocolPayloadCodec
    : IProtocolPayloadCodec
{
    public ProtocolEnvelope Encode(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            DiscoverRequest request =>
                new(
                    request.Version,
                    request.Role,
                    request.MessageType,
                    request.CorrelationId,
                    ReadOnlyMemory<byte>.Empty),

            DiscoverResponse response =>
                Encode(response),

            _ => throw new NotSupportedException(
                $"Encoding '{message.MessageType}' is not supported.")
        };
    }

    public ProtocolMessage Decode(
        ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Version != ProtocolVersion.Current)
        {
            throw new InvalidDataException(
                "Unsupported protocol version.");
        }

        return envelope.MessageType switch
        {
            ProtocolMessageType.DiscoverRequest =>
                DecodeDiscoverRequest(envelope),

            ProtocolMessageType.DiscoverResponse =>
                DecodeDiscoverResponse(envelope),

            _ => throw new NotSupportedException(
                $"Decoding '{envelope.MessageType}' is not supported.")
        };
    }

    private static ProtocolEnvelope Encode(
        DiscoverResponse response)
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(response.EndpointId.Value);
        writer.WriteCount(response.InstrumentIds.Count);

        foreach (InstrumentId id in response.InstrumentIds)
        {
            writer.WriteString(id.Value);
        }

        return new ProtocolEnvelope(
            response.Version,
            response.Role,
            response.MessageType,
            response.CorrelationId,
            writer.ToArray());
    }

    private static DiscoverRequest DecodeDiscoverRequest(
        ProtocolEnvelope envelope)
    {
        if (envelope.Role != ProtocolMessageRole.Request)
        {
            throw new InvalidDataException();
        }

        if (!envelope.Payload.IsEmpty)
        {
            throw new InvalidDataException();
        }

        return new DiscoverRequest(
            envelope.CorrelationId);
    }

    private static DiscoverResponse DecodeDiscoverResponse(
        ProtocolEnvelope envelope)
    {
        if (envelope.Role != ProtocolMessageRole.Response)
        {
            throw new InvalidDataException();
        }

        BinaryProtocolReader reader =
            new(envelope.Payload);

        EndpointId endpointId =
            new(reader.ReadString());

        int count = reader.ReadCount();

        List<InstrumentId> instrumentIds =
            new(count);

        for (int i = 0; i < count; i++)
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
}