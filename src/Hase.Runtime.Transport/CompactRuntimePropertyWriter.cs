using Hase.CompactProtocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Writes compact endpoint properties and updates the runtime cache only from a
/// successful endpoint confirmation read.
/// </summary>
internal sealed class CompactRuntimePropertyWriter
{
    private readonly CompactPropertyMap _propertyMap;
    private readonly CompactMappedPropertyWriter _propertyWriter;
    private readonly CompactPropertyReader _propertyReader;

    public CompactRuntimePropertyWriter(
        ICompactSerialProtocolConnection connection,
        CompactPropertyMap propertyMap)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _propertyWriter =
            new CompactMappedPropertyWriter(
                connection,
                propertyMap);

        _propertyReader =
            new CompactPropertyReader(
                connection,
                propertyMap);
    }

    public async Task<CompactRuntimePropertyWriteResult> WriteAsync(
        RuntimeEndpoint runtimeEndpoint,
        byte compactPropertyId,
        object value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

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

        RuntimeProperty runtimeProperty =
            ResolveRuntimeProperty(
                runtimeEndpoint,
                mapping);

        CompactPropertyWriteResult writeResult =
            await _propertyWriter.WriteAsync(
                compactPropertyId,
                value,
                cancellationToken);

        if (writeResult.Status
            != CompactPropertyWriteStatus.Success)
        {
            return new CompactRuntimePropertyWriteResult(
                mapping,
                runtimeProperty,
                writeResult.Status,
                confirmationReadStatus: null);
        }

        CompactPropertyReadResult readResult =
            await _propertyReader.ReadAsync(
                compactPropertyId,
                cancellationToken);

        if (readResult.Status
            == CompactPropertyReadStatus.Success)
        {
            runtimeProperty.UpdateValue(
                readResult.Value
                ?? throw new InvalidDataException(
                    "A successful compact confirmation read did not contain "
                    + "a property value."));
        }

        return new CompactRuntimePropertyWriteResult(
            mapping,
            runtimeProperty,
            writeResult.Status,
            readResult.Status);
    }

    private static RuntimeProperty ResolveRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint,
        CompactPropertyMapping mapping)
    {
        RuntimeInstrument runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                mapping.InstrumentId)
            ?? throw new InvalidDataException(
                $"Compact property identifier "
                + $"0x{mapping.CompactPropertyId:X2} maps to instrument "
                + $"'{mapping.InstrumentId.Value}', which is not present in "
                + "the runtime endpoint.");

        return runtimeInstrument.FindProperty(
                mapping.PropertyId)
            ?? throw new InvalidDataException(
                $"Compact property identifier "
                + $"0x{mapping.CompactPropertyId:X2} maps to property "
                + $"'{mapping.PropertyId.Value}', which is not present in "
                + $"runtime instrument '{mapping.InstrumentId.Value}'.");
    }
}