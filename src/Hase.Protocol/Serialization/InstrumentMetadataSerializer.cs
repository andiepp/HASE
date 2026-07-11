using Hase.Core.Domain.Instruments;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes instrument metadata using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class InstrumentMetadataSerializer
{
    private const byte NullMarker = 0;
    private const byte ValueMarker = 1;

    public void Write(
        BinaryProtocolWriter writer,
        InstrumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(metadata);

        WriteOptionalString(writer, metadata.Manufacturer);
        WriteOptionalString(writer, metadata.Model);
        WriteOptionalString(writer, metadata.SerialNumber);
        WriteOptionalString(writer, metadata.FirmwareVersion);
        WriteOptionalString(writer, metadata.HardwareRevision);
        WriteOptionalString(writer, metadata.Description);
    }

    public InstrumentMetadata Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new InstrumentMetadata
        {
            Manufacturer = ReadOptionalString(reader),
            Model = ReadOptionalString(reader),
            SerialNumber = ReadOptionalString(reader),
            FirmwareVersion = ReadOptionalString(reader),
            HardwareRevision = ReadOptionalString(reader),
            Description = ReadOptionalString(reader)
        };
    }

    private static void WriteOptionalString(
        BinaryProtocolWriter writer,
        string? value)
    {
        if (value is null)
        {
            writer.WriteByte(NullMarker);
            return;
        }

        writer.WriteByte(ValueMarker);
        writer.WriteString(value);
    }

    private static string? ReadOptionalString(
        BinaryProtocolReader reader)
    {
        byte marker = reader.ReadByte();

        return marker switch
        {
            NullMarker => null,
            ValueMarker => reader.ReadString(),

            _ => throw new InvalidDataException(
                $"Invalid optional-value marker '{marker}'.")
        };
    }
}