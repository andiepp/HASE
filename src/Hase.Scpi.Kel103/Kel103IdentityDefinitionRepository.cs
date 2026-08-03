using Hase.Core.Domain.Descriptors;

namespace Hase.Scpi.Kel103;

/// <summary>
/// Resolves the exact supported version of the KEL-103 identity definition.
/// </summary>
public sealed class Kel103IdentityDefinitionRepository : IEndpointDescriptorRepository
{
    private readonly InMemoryEndpointDescriptorRepository repository = new(
        [
            new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
                Kel103IdentityDefinition.Reference,
                Kel103IdentityDefinition.EndpointDefinition)
        ]);

    public ValueTask<EndpointDescriptorDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default) =>
        repository.FindAsync(reference, cancellationToken);
}
