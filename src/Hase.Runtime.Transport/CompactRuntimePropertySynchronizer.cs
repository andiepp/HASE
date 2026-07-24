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
    private readonly CompactRuntimePropertyReader _propertyReader;

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
            new CompactRuntimePropertyReader(
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

            CompactRuntimePropertySynchronizationResult readResult =
                await _propertyReader.ReadAsync(
                    runtimeEndpoint,
                    mapping.CompactPropertyId,
                    cancellationToken);

            results.Add(
                readResult);
        }

        return results;
    }
}