using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol;

/// <summary>
/// Resolves and encodes compact property writes through a predefined host-side
/// endpoint descriptor and its compact property mappings.
/// </summary>
internal sealed class CompactMappedPropertyWriter
{
    private readonly CompactPropertyWriter _writer;
    private readonly CompactPropertyMap _propertyMap;

    public CompactMappedPropertyWriter(
        ICompactSerialProtocolConnection connection,
        CompactPropertyMap propertyMap)
        : this(
            new CompactPropertyWriter(
                connection),
            propertyMap)
    {
    }

    internal CompactMappedPropertyWriter(
        CompactPropertyWriter writer,
        CompactPropertyMap propertyMap)
    {
        _writer =
            writer
            ?? throw new ArgumentNullException(
                nameof(writer));

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));
    }

    public async Task<CompactPropertyWriteResult> WriteAsync(
        byte compactPropertyId,
        object value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (compactPropertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactPropertyId),
                compactPropertyId,
                "A compact property identifier must be nonzero.");
        }

        CompactPropertyMapping mapping =
            _propertyMap.Find(
                compactPropertyId)
            ?? throw new ArgumentException(
                $"Compact property identifier 0x{compactPropertyId:X2} "
                + "is not present in the selected host-side descriptor.",
                nameof(compactPropertyId));

        PropertyDescriptor property =
            FindProperty(
                mapping);

        if ((property.AccessMode & PropertyAccessMode.Write)
            != PropertyAccessMode.Write)
        {
            throw new InvalidOperationException(
                $"Property '{property.Id.Value}' of instrument "
                + $"'{mapping.InstrumentId.Value}' is not writable.");
        }

        ReadOnlyMemory<byte> encodedValue =
            CompactPropertyValueEncoder.Encode(
                mapping.Encoding,
                value);

        CompactPropertyWriteStatus status =
            await _writer.WriteAsync(
                mapping.CompactPropertyId,
                encodedValue,
                cancellationToken);

        return new CompactPropertyWriteResult(
            mapping,
            status);
    }

    private PropertyDescriptor FindProperty(
        CompactPropertyMapping mapping)
    {
        InstrumentDescriptor instrument =
            _propertyMap.DescriptorDefinition.Instruments.Single(
                candidate =>
                    candidate.Id == mapping.InstrumentId);

        return instrument.Interface.Properties.Single(
            candidate =>
                candidate.Id == mapping.PropertyId);
    }
}