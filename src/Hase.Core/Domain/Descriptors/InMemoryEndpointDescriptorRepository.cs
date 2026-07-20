namespace Hase.Core.Domain.Descriptors;

/// <summary>
/// Resolves endpoint descriptor definitions from an immutable snapshot of
/// predefined host-side entries.
/// </summary>
public sealed class InMemoryEndpointDescriptorRepository
    : IEndpointDescriptorRepository
{
    private readonly IReadOnlyDictionary<
        DescriptorReference,
        EndpointDescriptorDefinition> _entries;

    public InMemoryEndpointDescriptorRepository(
        IEnumerable<KeyValuePair<
            DescriptorReference,
            EndpointDescriptorDefinition>> entries)
    {
        ArgumentNullException.ThrowIfNull(
            entries);

        var entryDictionary =
            new Dictionary<
                DescriptorReference,
                EndpointDescriptorDefinition>();

        foreach (KeyValuePair<
            DescriptorReference,
            EndpointDescriptorDefinition> entry in entries)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException(
                    "A descriptor repository entry must have a reference.",
                    nameof(entries));
            }

            if (entry.Value is null)
            {
                throw new ArgumentException(
                    "A descriptor repository entry must have a definition.",
                    nameof(entries));
            }

            if (!entryDictionary.TryAdd(
                entry.Key,
                entry.Value))
            {
                throw new ArgumentException(
                    $"The descriptor reference '{entry.Key.Id.Value}' "
                    + $"version {entry.Key.Version} is duplicated.",
                    nameof(entries));
            }
        }

        _entries =
            entryDictionary;
    }

    public ValueTask<EndpointDescriptorDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        cancellationToken.ThrowIfCancellationRequested();

        _entries.TryGetValue(
            reference,
            out EndpointDescriptorDefinition? definition);

        return ValueTask.FromResult(
            definition);
    }
}