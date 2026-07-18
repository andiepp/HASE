using Hase.Protocol;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Adapts a legacy request/response transport connection to the runtime
/// protocol-connection abstraction.
/// </summary>
public sealed class LegacyRuntimeProtocolConnection
    : IRuntimeProtocolConnection
{
    private readonly ITransportConnection _connection;

    private readonly BinaryProtocolPayloadCodec _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
        new();

    /// <summary>
    /// Initializes the adapter.
    /// </summary>
    public LegacyRuntimeProtocolConnection(
        ITransportConnection connection)
    {
        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));
    }

    /// <inheritdoc />
    public async Task<ProtocolMessage> SendAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Role
            != ProtocolMessageRole.Request)
        {
            throw new ArgumentException(
                "Only request-role protocol messages can be sent "
                + "through a runtime protocol connection.",
                nameof(request));
        }

        if (request.CorrelationId.IsNone)
        {
            throw new ArgumentException(
                "A runtime protocol request must have a nonzero "
                + "correlation identifier.",
                nameof(request));
        }

        ProtocolEnvelope requestEnvelope =
            _payloadCodec.Encode(
                request);

        byte[] requestFrame =
            _envelopeByteCodec.Encode(
                requestEnvelope);

        byte[] responseFrame =
            await _connection.ExchangeAsync(
                requestFrame,
                cancellationToken);

        ProtocolEnvelope responseEnvelope =
            _envelopeByteCodec.Decode(
                responseFrame);

        ProtocolMessage response =
            _payloadCodec.Decode(
                responseEnvelope);

        if (response.Role
            != ProtocolMessageRole.Response)
        {
            throw new InvalidDataException(
                "The transport returned a non-response "
                + "protocol message.");
        }

        if (response.CorrelationId
            != request.CorrelationId)
        {
            throw new InvalidDataException(
                "The protocol response correlation identifier "
                + "does not match its request.");
        }

        return response;
    }
}