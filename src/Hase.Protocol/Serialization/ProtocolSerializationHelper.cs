namespace Hase.Protocol.Serialization;

/// <summary>
/// Provides common helper methods used by protocol serializers.
/// </summary>
internal static class ProtocolSerializationHelper
{
    private const byte NullMarker = 0;
    private const byte ValueMarker = 1;

    public static void WriteOptionalString(
        BinaryProtocolWriter writer,
        string? value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteByte(NullMarker);
            return;
        }

        writer.WriteByte(ValueMarker);
        writer.WriteString(value);
    }

    public static string? ReadOptionalString(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        byte marker = reader.ReadByte();

        return marker switch
        {
            NullMarker => null,

            ValueMarker => reader.ReadString(),

            _ => throw new InvalidDataException(
                $"Invalid optional string marker '{marker}'.")
        };
    }
}