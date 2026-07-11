using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes instrument descriptors using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class InstrumentDescriptorSerializer
{
    private readonly InstrumentMetadataSerializer _metadataSerializer =
        new();

    private readonly InstrumentInterfaceSerializer _interfaceSerializer =
        new();

    public void Write(
        BinaryProtocolWriter writer,
        InstrumentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(descriptor);

        writer.WriteString(
            descriptor.Id.Value);

        writer.WriteString(
            descriptor.Name);

        writer.WriteString(
            descriptor.Kind.Name);

        _metadataSerializer.Write(
            writer,
            descriptor.Metadata);

        _interfaceSerializer.Write(
            writer,
            descriptor.Interface);
    }

    public InstrumentDescriptor Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        InstrumentId id =
            new(reader.ReadString());

        string name =
            reader.ReadString();

        InstrumentKind kind =
            new(reader.ReadString());

        InstrumentMetadata metadata =
            _metadataSerializer.Read(reader);

        InstrumentInterface instrumentInterface =
            _interfaceSerializer.Read(reader);

        return new InstrumentDescriptor(
            id,
            name,
            kind)
        {
            Metadata = metadata,
            Interface = instrumentInterface
        };
    }
}