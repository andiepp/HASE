using Hase.Core.Domain.Instruments;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes instrument metadata.
/// </summary>
internal sealed class InstrumentMetadataSerializer
{
    public void Write(
        BinaryProtocolWriter writer,
        InstrumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(metadata);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.Manufacturer);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.Model);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.SerialNumber);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.FirmwareVersion);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.HardwareRevision);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            metadata.Description);
    }

    public InstrumentMetadata Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new InstrumentMetadata
        {
            Manufacturer =
                ProtocolSerializationHelper.ReadOptionalString(reader),

            Model =
                ProtocolSerializationHelper.ReadOptionalString(reader),

            SerialNumber =
                ProtocolSerializationHelper.ReadOptionalString(reader),

            FirmwareVersion =
                ProtocolSerializationHelper.ReadOptionalString(reader),

            HardwareRevision =
                ProtocolSerializationHelper.ReadOptionalString(reader),

            Description =
                ProtocolSerializationHelper.ReadOptionalString(reader)
        };
    }
}