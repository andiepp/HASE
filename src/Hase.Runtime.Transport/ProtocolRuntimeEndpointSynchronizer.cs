using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Synchronizes a runtime endpoint through the HASE protocol.
/// </summary>
/// <remarks>
/// Synchronization first reads and validates the complete physical endpoint
/// descriptor. It then reads the current value of every readable property
/// and updates the corresponding runtime-property cache.
///
/// This synchronizer does not rebuild the runtime graph, retry failed
/// operations, or periodically refresh property values.
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
    /// Reads and validates the physical endpoint descriptor, then reads the
    /// current value of every readable runtime property.
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

        await SynchronizeDescriptorAsync(
            connection,
            runtimeEndpoint,
            cancellationToken);

        await SynchronizeReadablePropertiesAsync(
            connection,
            runtimeEndpoint,
            cancellationToken);
    }

    private async Task SynchronizeDescriptorAsync(
        ITransportConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken)
    {
        CorrelationId correlationId =
            CreateCorrelationId();

        var request =
            new ReadEndpointDescriptorRequest(
                correlationId,
                runtimeEndpoint.Descriptor.Id);

        ProtocolMessage responseMessage =
            await ExchangeAsync(
                connection,
                request,
                cancellationToken);

        if (responseMessage
            is not ReadEndpointDescriptorResponse response)
        {
            throw new InvalidDataException(
                "The endpoint response did not decode as a "
                + "ReadEndpointDescriptorResponse.");
        }

        ValidateCorrelationId(
            correlationId,
            response.CorrelationId,
            "endpoint-descriptor");

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

    private async Task SynchronizeReadablePropertiesAsync(
        ITransportConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken)
    {
        foreach (RuntimeInstrument runtimeInstrument
                 in runtimeEndpoint.Instruments)
        {
            foreach (RuntimeProperty runtimeProperty
                     in runtimeInstrument.Properties)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsReadable(
                        runtimeProperty.Descriptor))
                {
                    continue;
                }

                PropertyValue propertyValue =
                    await ReadPropertyAsync(
                        connection,
                        runtimeInstrument,
                        runtimeProperty,
                        cancellationToken);

                runtimeProperty.UpdateValue(
                    propertyValue);
            }
        }
    }

    private async Task<PropertyValue> ReadPropertyAsync(
        ITransportConnection connection,
        RuntimeInstrument runtimeInstrument,
        RuntimeProperty runtimeProperty,
        CancellationToken cancellationToken)
    {
        CorrelationId correlationId =
            CreateCorrelationId();

        var request =
            new ReadPropertyRequest(
                correlationId,
                runtimeInstrument.Descriptor.Id,
                runtimeProperty.Descriptor.Id);

        ProtocolMessage responseMessage =
            await ExchangeAsync(
                connection,
                request,
                cancellationToken);

        if (responseMessage
            is not ReadPropertyResponse response)
        {
            throw new InvalidDataException(
                $"The response for property "
                + $"'{runtimeProperty.Descriptor.Id.Value}' did not "
                + "decode as a ReadPropertyResponse.");
        }

        ValidateCorrelationId(
            correlationId,
            response.CorrelationId,
            $"property '{runtimeProperty.Descriptor.Id.Value}'");

        if (!response.Result.IsSuccess)
        {
            throw new InvalidDataException(
                $"The endpoint returned result "
                + $"'{response.Result.Code}' while reading property "
                + $"'{runtimeProperty.Descriptor.Id.Value}': "
                + $"{response.Result.Message ?? "(no message)"}.");
        }

        return response.PropertyValue
            ?? throw new InvalidDataException(
                $"The successful response for property "
                + $"'{runtimeProperty.Descriptor.Id.Value}' did not "
                + "contain a property value.");
    }

    private async Task<ProtocolMessage> ExchangeAsync(
        ITransportConnection connection,
        ProtocolMessage request,
        CancellationToken cancellationToken)
    {
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

        return _payloadCodec.Decode(
            responseEnvelope);
    }

    private static bool IsReadable(
        PropertyDescriptor descriptor)
    {
        return (
            descriptor.AccessMode
            & PropertyAccessMode.Read)
            == PropertyAccessMode.Read;
    }

    private static void ValidateCorrelationId(
        CorrelationId expected,
        CorrelationId actual,
        string operation)
    {
        if (actual != expected)
        {
            throw new InvalidDataException(
                $"The {operation} response correlation identifier "
                + "does not match the request.");
        }
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