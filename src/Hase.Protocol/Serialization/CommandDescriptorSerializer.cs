using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes command descriptors using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class CommandDescriptorSerializer
{
    public void Write(
        BinaryProtocolWriter writer,
        CommandDescriptor descriptor)
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

    public CommandDescriptor Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        DescriptorPath path =
            DescriptorPath.Parse(reader.ReadString());

        string displayName =
            reader.ReadString();

        string? description =
            ProtocolSerializationHelper.ReadOptionalString(reader);

        return new CommandDescriptor(
            path,
            displayName)
        {
            Description = description
        };
    }
}
