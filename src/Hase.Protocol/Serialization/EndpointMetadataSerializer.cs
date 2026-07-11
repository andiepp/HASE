using Hase.Core.Domain.Endpoints;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes endpoint metadata using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class EndpointMetadataSerializer
{
    private const byte NullMarker = 0;
    private const byte ValueMarker = 1;

    /// <summary>
    /// Writes endpoint metadata to the supplied protocol writer.
    /// </summary>
    public void Write(
        BinaryProtocolWriter writer,
        EndpointMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(metadata);

        WriteOptionalString(
            writer,
            metadata.DisplayName);

        WriteOptionalString(
            writer,
            metadata.Description);
    }

    /// <summary>
    /// Reads endpoint metadata from the supplied protocol reader.
    /// </summary>
    public EndpointMetadata Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        string? displayName =
            ReadOptionalString(reader);

        string? description =
            ReadOptionalString(reader);

        return new EndpointMetadata
        {
            DisplayName = displayName,
            Description = description
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