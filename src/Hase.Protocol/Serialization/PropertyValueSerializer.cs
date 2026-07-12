using System.Buffers.Binary;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes property values using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class PropertyValueSerializer
{
    private readonly VariantSerializer _variantSerializer =
        new();

    public void Write(
        BinaryProtocolWriter writer,
        PropertyValue propertyValue)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(propertyValue);

        _variantSerializer.Write(
            writer,
            propertyValue.Value);

        WriteInt64(
            writer,
            propertyValue.TimestampUtc.ToUnixTimeMilliseconds());

        writer.WriteByte(
            checked((byte)propertyValue.Quality));
    }

    public PropertyValue Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        object? value =
            _variantSerializer.Read(reader);

        long unixTimeMilliseconds =
            ReadInt64(reader);

        byte encodedQuality =
            reader.ReadByte();

        PropertyQuality quality =
            (PropertyQuality)encodedQuality;

        if (!Enum.IsDefined(quality))
        {
            throw new InvalidDataException(
                $"Unknown property quality '{encodedQuality}'.");
        }

        DateTimeOffset timestampUtc;

        try
        {
            timestampUtc =
                DateTimeOffset.FromUnixTimeMilliseconds(
                    unixTimeMilliseconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                $"Invalid Unix timestamp '{unixTimeMilliseconds}'.",
                exception);
        }

        return new PropertyValue(
            value,
            timestampUtc,
            quality);
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