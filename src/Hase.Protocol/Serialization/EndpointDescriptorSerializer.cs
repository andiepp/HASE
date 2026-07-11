using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes endpoint descriptors using the HASE
/// protocol version 1 binary encoding.
/// </summary>
internal sealed class EndpointDescriptorSerializer
{
    private readonly EndpointMetadataSerializer _metadataSerializer =
        new();

    private readonly InstrumentDescriptorSerializer _instrumentSerializer =
        new();

    public void Write(
        BinaryProtocolWriter writer,
        EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(descriptor);

        writer.WriteString(
            descriptor.Id.Value);

        _metadataSerializer.Write(
            writer,
            descriptor.Metadata);

        writer.WriteCount(
            descriptor.Instruments.Count);

        foreach (InstrumentDescriptor instrument in descriptor.Instruments)
        {
            _instrumentSerializer.Write(
                writer,
                instrument);
        }
    }

    public EndpointDescriptor Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        EndpointId endpointId =
            new(reader.ReadString());

        EndpointMetadata metadata =
            _metadataSerializer.Read(reader);

        int instrumentCount =
            reader.ReadCount();

        List<InstrumentDescriptor> instruments =
            new(instrumentCount);

        for (int index = 0; index < instrumentCount; index++)
        {
            instruments.Add(
                _instrumentSerializer.Read(reader));
        }

        return new EndpointDescriptor(
            endpointId,
            instruments)
        {
            Metadata = metadata
        };
    }
}