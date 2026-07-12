using System.Buffers.Binary;

namespace Hase.Protocol;

/// <summary>
/// Serializes and deserializes protocol variant values.
/// </summary>
internal sealed class VariantSerializer
{
    public void Write(
        BinaryProtocolWriter writer,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case null:
                writer.WriteByte(
                    (byte)VariantType.Null);
                return;

            case bool booleanValue:
                writer.WriteByte(
                    (byte)VariantType.Boolean);

                writer.WriteByte(
                    booleanValue ? (byte)1 : (byte)0);
                return;

            case int int32Value:
                writer.WriteByte(
                    (byte)VariantType.Int32);

                WriteInt32(
                    writer,
                    int32Value);
                return;

            case long int64Value:
                writer.WriteByte(
                    (byte)VariantType.Int64);

                WriteInt64(
                    writer,
                    int64Value);
                return;

            case double doubleValue:
                writer.WriteByte(
                    (byte)VariantType.Double);

                writer.WriteDouble(
                    doubleValue);
                return;

            case string stringValue:
                writer.WriteByte(
                    (byte)VariantType.String);

                writer.WriteString(
                    stringValue);
                return;

            default:
                throw new NotSupportedException(
                    $"CLR type '{value.GetType().FullName}' is not " +
                    "supported by the protocol variant serializer.");
        }
    }

    public object? Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        byte encodedType =
            reader.ReadByte();

        VariantType variantType =
            (VariantType)encodedType;

        return variantType switch
        {
            VariantType.Null =>
                null,

            VariantType.Boolean =>
                ReadBoolean(reader),

            VariantType.Int32 =>
                ReadInt32(reader),

            VariantType.Int64 =>
                ReadInt64(reader),

            VariantType.Double =>
                reader.ReadDouble(),

            VariantType.String =>
                reader.ReadString(),

            _ => throw new InvalidDataException(
                $"Unknown protocol variant type '{encodedType}'.")
        };
    }

    private static bool ReadBoolean(
        BinaryProtocolReader reader)
    {
        byte value =
            reader.ReadByte();

        return value switch
        {
            0 => false,
            1 => true,

            _ => throw new InvalidDataException(
                $"Invalid protocol Boolean value '{value}'.")
        };
    }

    private static void WriteInt32(
        BinaryProtocolWriter writer,
        int value)
    {
        Span<byte> bytes =
            stackalloc byte[sizeof(int)];

        BinaryPrimitives.WriteInt32LittleEndian(
            bytes,
            value);

        foreach (byte item in bytes)
        {
            writer.WriteByte(item);
        }
    }

    private static int ReadInt32(
        BinaryProtocolReader reader)
    {
        Span<byte> bytes =
            stackalloc byte[sizeof(int)];

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] =
                reader.ReadByte();
        }

        return BinaryPrimitives.ReadInt32LittleEndian(
            bytes);
    }

    private static void WriteInt64(
        BinaryProtocolWriter writer,
        long value)
    {
        Span<byte> bytes =
            stackalloc byte[sizeof(long)];

        BinaryPrimitives.WriteInt64LittleEndian(
            bytes,
            value);

        foreach (byte item in bytes)
        {
            writer.WriteByte(item);
        }
    }

    private static long ReadInt64(
        BinaryProtocolReader reader)
    {
        Span<byte> bytes =
            stackalloc byte[sizeof(long)];

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] =
                reader.ReadByte();
        }

        return BinaryPrimitives.ReadInt64LittleEndian(
            bytes);
    }
}