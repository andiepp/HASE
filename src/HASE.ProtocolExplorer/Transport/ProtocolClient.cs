using Hase.Protocol;

namespace Hase.ProtocolExplorer.Transport;

/// <summary>
/// Executes protocol request/response exchanges through a raw-byte
/// transport.
/// </summary>
internal sealed class ProtocolClient
{
    private readonly IProtocolTransport _transport;

    private readonly BinaryProtocolPayloadCodec
        _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec
        _envelopeCodec =
        new();

    public ProtocolClient(
        IProtocolTransport transport)
    {
        _transport =
            transport
            ?? throw new ArgumentNullException(
                nameof(transport));
    }

    public async Task<ProtocolExchangeResult> SendAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Role != ProtocolMessageRole.Request)
        {
            throw new ArgumentException(
                $"Only protocol requests can be sent through the " +
                $"protocol client. Message type " +
                $"'{request.MessageType}' has role " +
                $"'{request.Role}'.",
                nameof(request));
        }

        ProtocolEnvelope requestEnvelope =
            _payloadCodec.Encode(
                request);

        byte[] requestFrame =
            _envelopeCodec.Encode(
                requestEnvelope);

        byte[] responseFrame =
            await _transport.SendAsync(
                requestFrame,
                cancellationToken);

        ProtocolEnvelope responseEnvelope =
            _envelopeCodec.Decode(
                responseFrame);

        ProtocolMessage responseMessage =
            _payloadCodec.Decode(
                responseEnvelope);

        ValidateResponse(
            request,
            responseMessage);

        return new ProtocolExchangeResult(
            request,
            requestEnvelope,
            requestFrame,
            responseFrame,
            responseEnvelope,
            responseMessage);
    }

    private static void ValidateResponse(
        ProtocolMessage request,
        ProtocolMessage response)
    {
        if (response.Role != ProtocolMessageRole.Response)
        {
            throw new InvalidDataException(
                $"The transport returned message type " +
                $"'{response.MessageType}' with role " +
                $"'{response.Role}' instead of a response.");
        }

        if (response.CorrelationId !=
            request.CorrelationId)
        {
            throw new InvalidDataException(
                $"The response correlation identifier " +
                $"'{response.CorrelationId}' does not match the " +
                $"request correlation identifier " +
                $"'{request.CorrelationId}'.");
        }
    }
}