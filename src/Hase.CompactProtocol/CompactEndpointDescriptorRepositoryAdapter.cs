using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

/// <summary>
/// Projects compact endpoint definitions into the transport-independent
/// endpoint descriptor-repository contract used by compact bootstrap.
/// </summary>
public sealed class CompactEndpointDescriptorRepositoryAdapter
    : IEndpointDescriptorRepository
{
    private readonly ICompactEndpointDefinitionRepository _repository;

    /// <summary>
    /// Initializes a descriptor-repository projection over one compact
    /// endpoint-definition repository.
    /// </summary>
    public CompactEndpointDescriptorRepositoryAdapter(
        ICompactEndpointDefinitionRepository repository)
    {
        _repository =
            repository
            ?? throw new ArgumentNullException(
                nameof(repository));
    }

    /// <inheritdoc />
    public async ValueTask<EndpointDescriptorDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        CompactEndpointDefinition? definition =
            await _repository.FindAsync(
                reference,
                cancellationToken);

        return definition?.DescriptorDefinition;
    }
}