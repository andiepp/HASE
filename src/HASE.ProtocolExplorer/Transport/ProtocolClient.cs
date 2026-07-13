using Hase.Protocol;
using Hase.Transport;

namespace Hase.ProtocolExplorer.Transport;

/// <summary>
/// Executes protocol request/response exchanges through an
/// <see cref="ITransportConnection"/>.
/// </summary>
internal sealed class ProtocolClient
{
    private readonly ITransportConnection
        _transportConnection;

    private readonly BinaryProtocolPayloadCodec
        _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec
        _envelopeByteCodec =
        new();

    public ProtocolClient(
        ITransportConnection transportConnection)
    {
        _transportConnection =
            transportConnection
            ?? throw new ArgumentNullException(
                nameof(transportConnection));
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
                "Only request messages can be sent.",
                nameof(request));
        }

        ProtocolEnvelope requestEnvelope =
            _payloadCodec.Encode(
                request);

        byte[] requestFrame =
            _envelopeByteCodec.Encode(
                requestEnvelope);

        byte[] responseFrame =
            await _transportConnection.ExchangeAsync(
                requestFrame,
                cancellationToken);

        ProtocolEnvelope responseEnvelope =
            _envelopeByteCodec.Decode(
                responseFrame);

        ProtocolMessage responseMessage =
            _payloadCodec.Decode(
                responseEnvelope);

        ValidateExchange(
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

    private static void ValidateExchange(
        ProtocolMessage request,
        ProtocolMessage response)
    {
        if (response.Role != ProtocolMessageRole.Response)
        {
            throw new InvalidDataException(
                "Transport returned a non-response message.");
        }

        if (response.CorrelationId != request.CorrelationId)
        {
            throw new InvalidDataException(
                "Response correlation identifier does not match the request.");
        }
    }
}