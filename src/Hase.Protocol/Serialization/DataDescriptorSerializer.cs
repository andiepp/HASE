using Hase.Core.Domain.Data;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes data descriptors using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class DataDescriptorSerializer
{
    private const byte NullMarker = 0;
    private const byte ValueMarker = 1;

    private const byte StringDescriptorType = 1;
    private const byte NumericDescriptorType = 2;

    /// <summary>
    /// Writes a data descriptor to the supplied protocol writer.
    /// </summary>
    public void Write(
        BinaryProtocolWriter writer,
        DataDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(descriptor);

        switch (descriptor)
        {
            case StringDataDescriptor:
                writer.WriteByte(StringDescriptorType);
                break;

            case NumericDataDescriptor numericDescriptor:
                WriteNumericDescriptor(
                    writer,
                    numericDescriptor);
                break;

            default:
                throw new NotSupportedException(
                    $"Data descriptor type " +
                    $"'{descriptor.GetType().Name}' is not supported.");
        }
    }

    /// <summary>
    /// Reads a data descriptor from the supplied protocol reader.
    /// </summary>
    public DataDescriptor Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        byte descriptorType =
            reader.ReadByte();

        return descriptorType switch
        {
            StringDescriptorType =>
                new StringDataDescriptor(),

            NumericDescriptorType =>
                ReadNumericDescriptor(reader),

            _ => throw new InvalidDataException(
                $"Unknown data descriptor type " +
                $"'{descriptorType}'.")
        };
    }

    private static void WriteNumericDescriptor(
        BinaryProtocolWriter writer,
        NumericDataDescriptor descriptor)
    {
        writer.WriteByte(NumericDescriptorType);

        writer.WriteString(
            descriptor.Quantity.Id);

        writer.WriteString(
            descriptor.Quantity.DisplayName);

        writer.WriteString(
            descriptor.NativeUnit.Id);

        writer.WriteString(
            descriptor.NativeUnit.DisplayName);

        writer.WriteString(
            descriptor.NativeUnit.Symbol);

        WriteOptionalRange(
            writer,
            descriptor.Range);

        WriteOptionalResolution(
            writer,
            descriptor.Resolution);
    }

    private static NumericDataDescriptor ReadNumericDescriptor(
        BinaryProtocolReader reader)
    {
        Quantity quantity = new(
            reader.ReadString(),
            reader.ReadString());

        Unit nativeUnit = new(
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            quantity);

        ValueRange? range =
            ReadOptionalRange(reader);

        Resolution? resolution =
            ReadOptionalResolution(reader);

        return new NumericDataDescriptor(
            quantity,
            nativeUnit,
            range,
            resolution);
    }

    private static void WriteOptionalRange(
        BinaryProtocolWriter writer,
        ValueRange? range)
    {
        if (range is null)
        {
            writer.WriteByte(NullMarker);
            return;
        }

        writer.WriteByte(ValueMarker);
        writer.WriteDouble(range.Minimum);
        writer.WriteDouble(range.Maximum);
    }

    private static ValueRange? ReadOptionalRange(
        BinaryProtocolReader reader)
    {
        byte marker =
            reader.ReadByte();

        return marker switch
        {
            NullMarker => null,

            ValueMarker => new ValueRange(
                reader.ReadDouble(),
                reader.ReadDouble()),

            _ => throw new InvalidDataException(
                $"Invalid optional range marker '{marker}'.")
        };
    }

    private static void WriteOptionalResolution(
        BinaryProtocolWriter writer,
        Resolution? resolution)
    {
        if (resolution is null)
        {
            writer.WriteByte(NullMarker);
            return;
        }

        writer.WriteByte(ValueMarker);
        writer.WriteDouble(resolution.Value);
    }

    private static Resolution? ReadOptionalResolution(
        BinaryProtocolReader reader)
    {
        byte marker =
            reader.ReadByte();

        return marker switch
        {
            NullMarker => null,

            ValueMarker => new Resolution(
                reader.ReadDouble()),

            _ => throw new InvalidDataException(
                $"Invalid optional resolution marker '{marker}'.")
        };
    }
}