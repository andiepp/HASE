namespace Hase.Protocol;

/// <summary>
/// Encodes and decodes the binary payloads of HASE protocol messages.
/// </summary>
public sealed class BinaryProtocolPayloadCodec
    : IProtocolPayloadCodec
{
    /// <inheritdoc />
    public ProtocolEnvelope Encode(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            DiscoverRequest request =>
                EncodeDiscoverRequest(request),

            _ => throw new NotSupportedException(
                $"Encoding message type '{message.MessageType}' " +
                "is not supported.")
        };
    }

    /// <inheritdoc />
    public ProtocolMessage Decode(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        ValidateVersion(envelope.Version);

        return envelope.MessageType switch
        {
            ProtocolMessageType.DiscoverRequest =>
                DecodeDiscoverRequest(envelope),

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
        if (envelope.Role != ProtocolMessageRole.Request)
        {
            throw new InvalidDataException(
                "A DiscoverRequest envelope must have the Request role.");
        }

        if (!envelope.Payload.IsEmpty)
        {
            throw new InvalidDataException(
                "A DiscoverRequest envelope must have an empty payload.");
        }

        return new DiscoverRequest(
            envelope.CorrelationId);
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
}