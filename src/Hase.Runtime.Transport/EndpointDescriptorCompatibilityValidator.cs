using Hase.Core.Domain.Endpoints;
using Hase.Protocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Validates that a physical endpoint descriptor is strictly compatible
/// with the descriptor used to construct an existing runtime endpoint.
/// </summary>
/// <remarks>
/// Compatibility is defined by equality of the complete canonical protocol
/// envelope produced for each descriptor.
///
/// This includes endpoint metadata, instruments, instrument metadata,
/// properties, commands, events, and their nested data descriptors.
/// Collection ordering is significant because it is significant in the
/// protocol representation.
/// </remarks>
public sealed class EndpointDescriptorCompatibilityValidator
{
    private static readonly CorrelationId
        ValidationCorrelationId =
            new(1);

    private readonly BinaryProtocolPayloadCodec _payloadCodec =
        new();

    /// <summary>
    /// Validates that the physical descriptor matches the runtime
    /// descriptor.
    /// </summary>
    /// <param name="runtimeDescriptor">
    /// Descriptor used to construct the existing runtime endpoint.
    /// </param>
    /// <param name="physicalDescriptor">
    /// Descriptor read from the physical endpoint.
    /// </param>
    /// <exception cref="InvalidDataException">
    /// The endpoint identifiers differ or the complete serialized
    /// descriptors are not equal.
    /// </exception>
    public void Validate(
        EndpointDescriptor runtimeDescriptor,
        EndpointDescriptor physicalDescriptor)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeDescriptor);

        ArgumentNullException.ThrowIfNull(
            physicalDescriptor);

        if (runtimeDescriptor.Id
            != physicalDescriptor.Id)
        {
            throw new InvalidDataException(
                $"The physical endpoint identifier "
                + $"'{physicalDescriptor.Id.Value}' does not match "
                + $"the runtime endpoint identifier "
                + $"'{runtimeDescriptor.Id.Value}'.");
        }

        ProtocolEnvelope runtimeEnvelope =
            EncodeDescriptor(
                runtimeDescriptor);

        ProtocolEnvelope physicalEnvelope =
            EncodeDescriptor(
                physicalDescriptor);

        if (!AreEqual(
                runtimeEnvelope,
                physicalEnvelope))
        {
            throw new InvalidDataException(
                $"The physical descriptor for endpoint "
                + $"'{physicalDescriptor.Id.Value}' is not strictly "
                + "compatible with the existing runtime descriptor.");
        }
    }

    private ProtocolEnvelope EncodeDescriptor(
        EndpointDescriptor descriptor)
    {
        var response =
            new ReadEndpointDescriptorResponse(
                ValidationCorrelationId,
                ProtocolResult.Success,
                descriptor);

        return _payloadCodec.Encode(
            response);
    }

    private static bool AreEqual(
        ProtocolEnvelope first,
        ProtocolEnvelope second)
    {
        return first.Version
                   == second.Version
               && first.Role
                   == second.Role
               && first.MessageType
                   == second.MessageType
               && first.CorrelationId
                   == second.CorrelationId
               && first.PayloadLength
                   == second.PayloadLength
               && first.Payload
                   .Span
                   .SequenceEqual(
                       second.Payload.Span);
    }
}