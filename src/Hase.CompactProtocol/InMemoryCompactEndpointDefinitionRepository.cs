using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

/// <summary>
/// Resolves compact endpoint definitions from an immutable snapshot of
/// predefined host-side registrations.
/// </summary>
public sealed class InMemoryCompactEndpointDefinitionRepository
    : ICompactEndpointDefinitionRepository
{
    private readonly IReadOnlyDictionary<
        DescriptorReference,
        CompactEndpointDefinition> _definitions;

    /// <summary>
    /// Initializes an immutable in-memory compact endpoint-definition
    /// repository.
    /// </summary>
    public InMemoryCompactEndpointDefinitionRepository(
        IEnumerable<CompactEndpointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(
            definitions);

        var definitionDictionary =
            new Dictionary<
                DescriptorReference,
                CompactEndpointDefinition>();

        foreach (CompactEndpointDefinition definition in definitions)
        {
            if (definition is null)
            {
                throw new ArgumentException(
                    "A compact endpoint-definition repository entry must not "
                    + "be null.",
                    nameof(definitions));
            }

            if (!definitionDictionary.TryAdd(
                    definition.DescriptorReference,
                    definition))
            {
                throw new ArgumentException(
                    $"The compact endpoint definition "
                    + $"'{definition.DescriptorReference.Id.Value}' version "
                    + $"{definition.DescriptorReference.Version} is "
                    + "duplicated.",
                    nameof(definitions));
            }
        }

        _definitions =
            definitionDictionary;
    }

    /// <inheritdoc />
    public ValueTask<CompactEndpointDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        cancellationToken.ThrowIfCancellationRequested();

        _definitions.TryGetValue(
            reference,
            out CompactEndpointDefinition? definition);

        return ValueTask.FromResult(
            definition);
    }
}