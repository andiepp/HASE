using Hase.Core.Domain.Endpoints;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Synchronizes a runtime endpoint by reading and validating the complete
/// endpoint descriptor through the HASE protocol.
/// </summary>
/// <remarks>
/// This synchronizer does not modify or rebuild the runtime graph.
/// Synchronization succeeds only when the physical descriptor is strictly
/// compatible with the descriptor already used by the runtime endpoint.
/// </remarks>
public sealed class ProtocolRuntimeEndpointSynchronizer
    : IRuntimeEndpointSynchronizer
{
    private static int _nextCorrelationId;

    private readonly EndpointDescriptorCompatibilityValidator
        _compatibilityValidator;

    private readonly BinaryProtocolPayloadCodec _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
        new();

    /// <summary>
    /// Initializes a protocol runtime-endpoint synchronizer.
    /// </summary>
    public ProtocolRuntimeEndpointSynchronizer(
        EndpointDescriptorCompatibilityValidator compatibilityValidator)
    {
        _compatibilityValidator =
            compatibilityValidator
            ?? throw new ArgumentNullException(
                nameof(compatibilityValidator));
    }

    /// <summary>
    /// Reads the physical endpoint descriptor and validates it against the
    /// existing runtime descriptor.
    /// </summary>
    public async Task SynchronizeAsync(
        ITransportConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        cancellationToken.ThrowIfCancellationRequested();

        CorrelationId correlationId =
            CreateCorrelationId();

        var request =
            new ReadEndpointDescriptorRequest(
                correlationId,
                runtimeEndpoint.Descriptor.Id);

        ProtocolEnvelope requestEnvelope =
            _payloadCodec.Encode(
                request);

        byte[] requestFrame =
            _envelopeByteCodec.Encode(
                requestEnvelope);

        byte[] responseFrame =
            await connection.ExchangeAsync(
                requestFrame,
                cancellationToken);

        ProtocolEnvelope responseEnvelope =
            _envelopeByteCodec.Decode(
                responseFrame);

        ProtocolMessage responseMessage =
            _payloadCodec.Decode(
                responseEnvelope);

        if (responseMessage
            is not ReadEndpointDescriptorResponse response)
        {
            throw new InvalidDataException(
                "The endpoint response did not decode as a "
                + "ReadEndpointDescriptorResponse.");
        }

        if (response.CorrelationId
            != correlationId)
        {
            throw new InvalidDataException(
                "The endpoint-descriptor response correlation "
                + "identifier does not match the request.");
        }

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                "The endpoint returned descriptor result "
                + $"'{response.Result.Code}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        EndpointDescriptor physicalDescriptor =
            response.Descriptor
            ?? throw new InvalidDataException(
                "The successful endpoint-descriptor response did not "
                + "contain a descriptor.");

        _compatibilityValidator.Validate(
            runtimeEndpoint.Descriptor,
            physicalDescriptor);
    }

    private static CorrelationId CreateCorrelationId()
    {
        uint value =
            unchecked(
                (uint)Interlocked.Increment(
                    ref _nextCorrelationId));

        if (value == CorrelationId.None.Value)
        {
            value =
                unchecked(
                    (uint)Interlocked.Increment(
                        ref _nextCorrelationId));
        }

        return new CorrelationId(
            value);
    }
}