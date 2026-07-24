using Hase.CompactProtocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Reads one compact endpoint Property and updates the runtime cache only from
/// a successful authoritative response.
/// </summary>
internal sealed class CompactRuntimePropertyReader
{
    private readonly CompactPropertyMap _propertyMap;
    private readonly CompactPropertyReader _propertyReader;

    public CompactRuntimePropertyReader(
        ICompactSerialProtocolConnection connection,
        CompactPropertyMap propertyMap)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _propertyReader =
            new CompactPropertyReader(
                connection,
                propertyMap);
    }

    public async Task<CompactRuntimePropertySynchronizationResult> ReadAsync(
        RuntimeEndpoint runtimeEndpoint,
        byte compactPropertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        cancellationToken.ThrowIfCancellationRequested();

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
                    "A successful compact property read did not contain "
                    + "a property value."));
        }

        return new CompactRuntimePropertySynchronizationResult(
            mapping,
            runtimeProperty,
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