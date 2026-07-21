using Hase.CompactProtocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Reads the properties declared by a compact property map and synchronizes
/// successful values into an existing runtime endpoint property cache.
/// </summary>
internal sealed class CompactRuntimePropertySynchronizer
{
    private readonly CompactPropertyMap _propertyMap;
    private readonly CompactPropertyReader _propertyReader;

    public CompactRuntimePropertySynchronizer(
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

    public async Task<
        IReadOnlyList<CompactRuntimePropertySynchronizationResult>>
        SynchronizeAsync(
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        cancellationToken.ThrowIfCancellationRequested();

        var results =
            new List<CompactRuntimePropertySynchronizationResult>(
                _propertyMap.Mappings.Count);

        foreach (CompactPropertyMapping mapping
                 in _propertyMap.Mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RuntimeProperty runtimeProperty =
                ResolveRuntimeProperty(
                    runtimeEndpoint,
                    mapping);

            CompactPropertyReadResult readResult =
                await _propertyReader.ReadAsync(
                    mapping.CompactPropertyId,
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

            results.Add(
                new CompactRuntimePropertySynchronizationResult(
                    mapping,
                    runtimeProperty,
                    readResult.Status));
        }

        return results;
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