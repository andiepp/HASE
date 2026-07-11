using Hase.Core.Domain.Events;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes event descriptors using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class EventDescriptorSerializer
{
    public void Write(
        BinaryProtocolWriter writer,
        EventDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(descriptor);

        writer.WriteString(
            descriptor.Path.ToString());

        writer.WriteString(
            descriptor.DisplayName);

        ProtocolSerializationHelper.WriteOptionalString(
            writer,
            descriptor.Description);
    }

    public EventDescriptor Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        DescriptorPath path =
            DescriptorPath.Parse(
                reader.ReadString());

        string displayName =
            reader.ReadString();

        string? description =
            ProtocolSerializationHelper.ReadOptionalString(
                reader);

        return new EventDescriptor(
            path,
            displayName)
        {
            Description = description
        };
    }
}