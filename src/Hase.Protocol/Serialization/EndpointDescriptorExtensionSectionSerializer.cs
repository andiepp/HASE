namespace Hase.Protocol.Serialization;

/// <summary>
/// Serializes and deserializes the optional length-delimited endpoint
/// descriptor extension section.
/// </summary>
internal sealed class EndpointDescriptorExtensionSectionSerializer
{
    public void Write(
        BinaryProtocolWriter writer,
        IReadOnlyList<EndpointDescriptorExtension> extensions)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(extensions);

        if (extensions.Count == 0)
        {
            throw new ArgumentException(
                "An endpoint descriptor extension section must contain " +
                "at least one extension.",
                nameof(extensions));
        }

        writer.WriteCount(
            extensions.Count);

        foreach (EndpointDescriptorExtension extension in extensions)
        {
            if (extension is null)
            {
                throw new ArgumentException(
                    "An endpoint descriptor extension must not be null.",
                    nameof(extensions));
            }

            writer.WriteByte(
                extension.Type);

            writer.WriteByteArray(
                extension.Payload);
        }
    }

    public IReadOnlyList<EndpointDescriptorExtension> Read(
        BinaryProtocolReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        int extensionCount =
            reader.ReadCount();

        if (extensionCount == 0)
        {
            throw new InvalidDataException(
                "An endpoint descriptor extension section must contain " +
                "at least one extension.");
        }

        List<EndpointDescriptorExtension> extensions =
            new(extensionCount);

        for (int index = 0; index < extensionCount; index++)
        {
            byte type =
                reader.ReadByte();

            byte[] payload =
                reader.ReadByteArray();

            if (payload.Length == 0)
            {
                throw new InvalidDataException(
                    $"Endpoint descriptor extension type '{type}' has an " +
                    "empty payload.");
            }

            extensions.Add(
                new EndpointDescriptorExtension(
                    type,
                    payload));
        }

        return extensions;
    }
}
