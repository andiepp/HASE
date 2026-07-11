namespace Hase.Protocol;

/// <summary>
/// Encodes protocol messages into envelopes and decodes envelopes back
/// into protocol messages.
/// </summary>
public interface IProtocolPayloadCodec
{
    /// <summary>
    /// Encodes a protocol message into an envelope containing its
    /// serialized payload.
    /// </summary>
    /// <param name="message">
    /// The protocol message to encode.
    /// </param>
    /// <returns>
    /// The encoded protocol envelope.
    /// </returns>
    ProtocolEnvelope Encode(ProtocolMessage message);

    /// <summary>
    /// Decodes a protocol envelope into its strongly typed protocol
    /// message.
    /// </summary>
    /// <param name="envelope">
    /// The protocol envelope to decode.
    /// </param>
    /// <returns>
    /// The decoded protocol message.
    /// </returns>
    ProtocolMessage Decode(ProtocolEnvelope envelope);
}