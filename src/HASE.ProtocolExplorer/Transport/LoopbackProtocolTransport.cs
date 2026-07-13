using Hase.Protocol;
using Hase.Runtime.Protocol;

namespace Hase.ProtocolExplorer.Transport;

/// <summary>
/// Executes a complete protocol request/response exchange in memory.
///
/// The transport receives and returns raw frame bytes. Protocol decoding,
/// runtime dispatch and response encoding happen inside the loopback
/// endpoint, as they would on a remote endpoint.
/// </summary>
internal sealed class LoopbackProtocolTransport
    : IProtocolTransport
{
    private readonly IRuntimeProtocolDispatcher
        _dispatcher;

    private readonly BinaryProtocolPayloadCodec
        _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec
        _envelopeCodec =
        new();

    public LoopbackProtocolTransport(
        IRuntimeProtocolDispatcher dispatcher)
    {
        _dispatcher =
            dispatcher
            ?? throw new ArgumentNullException(
                nameof(dispatcher));
    }

    public async Task<byte[]> SendAsync(
        byte[] request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        ProtocolEnvelope requestEnvelope =
            _envelopeCodec.Decode(
                request);

        ProtocolMessage requestMessage =
            _payloadCodec.Decode(
                requestEnvelope);

        ProtocolMessage responseMessage =
            await DispatchAsync(
                requestMessage,
                cancellationToken);

        ProtocolEnvelope responseEnvelope =
            _payloadCodec.Encode(
                responseMessage);

        return _envelopeCodec.Encode(
            responseEnvelope);
    }

    private async Task<ProtocolMessage> DispatchAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken)
    {
        return request switch
        {
            DiscoverRequest discoverRequest =>
                await _dispatcher.DispatchAsync(
                    discoverRequest,
                    cancellationToken),

            ReadEndpointDescriptorRequest
                readEndpointDescriptorRequest =>
                await _dispatcher.DispatchAsync(
                    readEndpointDescriptorRequest,
                    cancellationToken),

            ReadPropertyRequest readPropertyRequest =>
                await _dispatcher.DispatchAsync(
                    readPropertyRequest,
                    cancellationToken),

            WritePropertyRequest writePropertyRequest =>
                await _dispatcher.DispatchAsync(
                    writePropertyRequest,
                    cancellationToken),

            ExecuteCommandRequest executeCommandRequest =>
                await _dispatcher.DispatchAsync(
                    executeCommandRequest,
                    cancellationToken),

            _ =>
                throw new NotSupportedException(
                    $"Loopback dispatch does not support protocol " +
                    $"message type '{request.MessageType}' with role " +
                    $"'{request.Role}'.")
        };
    }
}