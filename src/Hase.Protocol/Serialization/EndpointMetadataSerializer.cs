using Hase.Core.Domain.Endpoints;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes endpoint metadata.
/// </summary>
internal sealed class EndpointMetadataSerializer
{
    public void Write(
        BinaryProtocolWriter writer,
        EndpointMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(metadata);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.DisplayName);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.Description);
    }

    public EndpointMetadata Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new EndpointMetadata
        {
            DisplayName =
                ProtocolSerializationHelper.ReadOptionalString(reader),

            Description =
                ProtocolSerializationHelper.ReadOptionalString(reader)
        };
    }
}