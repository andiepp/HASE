using Hase.Protocol;

namespace Hase.ProtocolExplorer.Transport;

/// <summary>
/// Contains every representation involved in one complete protocol
/// request/response exchange.
/// </summary>
internal sealed record ProtocolExchangeResult(
    ProtocolMessage RequestMessage,
    ProtocolEnvelope RequestEnvelope,
    ReadOnlyMemory<byte> RequestFrame,
    ReadOnlyMemory<byte> ResponseFrame,
    ProtocolEnvelope ResponseEnvelope,
    ProtocolMessage ResponseMessage);