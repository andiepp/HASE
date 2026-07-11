using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes property descriptors using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class PropertyDescriptorSerializer
{
    private readonly DataDescriptorSerializer _dataDescriptorSerializer =
        new();

    public void Write(
        BinaryProtocolWriter writer,
        PropertyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(descriptor);

        writer.WriteString(
            descriptor.Id.Value);

        writer.WriteString(
            descriptor.Path.ToString());

        writer.WriteString(
            descriptor.DisplayName);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            descriptor.Description);

        writer.WriteByte(
            checked((byte)descriptor.AccessMode));

        _dataDescriptorSerializer.Write(
            writer,
            descriptor.Data);
    }

    public PropertyDescriptor Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        PropertyId id =
            new(reader.ReadString());

        DescriptorPath path =
            DescriptorPath.Parse(
                reader.ReadString());

        string displayName =
            reader.ReadString();

        string? description =
            ProtocolSerializationHelper.ReadOptionalString(
                reader);

        PropertyAccessMode accessMode =
            ReadAccessMode(reader);

        var dataDescriptor =
            _dataDescriptorSerializer.Read(reader);

        return new PropertyDescriptor(
            id,
            path,
            displayName,
            dataDescriptor)
        {
            Description = description,
            AccessMode = accessMode
        };
    }

    private static PropertyAccessMode ReadAccessMode(
        BinaryProtocolReader reader)
    {
        byte encodedValue =
            reader.ReadByte();

        PropertyAccessMode accessMode =
            (PropertyAccessMode)encodedValue;

        if (!Enum.IsDefined(accessMode))
        {
            throw new InvalidDataException(
                $"Unknown property access mode '{encodedValue}'.");
        }

        return accessMode;
    }
}