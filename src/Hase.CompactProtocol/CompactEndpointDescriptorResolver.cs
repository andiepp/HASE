using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;

namespace Hase.CompactProtocol;

/// <summary>
/// Resolves the exact descriptor reference declared during compact bootstrap
/// and materializes it with the authoritative endpoint identity.
/// </summary>
internal sealed class CompactEndpointDescriptorResolver
{
    private readonly IEndpointDescriptorRepository _repository;

    public CompactEndpointDescriptorResolver(
        IEndpointDescriptorRepository repository)
    {
        _repository =
            repository
            ?? throw new ArgumentNullException(
                nameof(repository));
    }

    public async Task<EndpointDescriptorDefinition> ResolveDefinitionAsync(
        CompactBootstrapResponse bootstrapResponse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            bootstrapResponse);

        cancellationToken.ThrowIfCancellationRequested();

        EndpointDescriptorDefinition? definition =
            await _repository.FindAsync(
                bootstrapResponse.DescriptorReference,
                cancellationToken);

        if (definition is null)
        {
            DescriptorReference reference =
                bootstrapResponse.DescriptorReference;

            throw new InvalidDataException(
                $"Descriptor '{reference.Id.Value}' version "
                + $"{reference.Version} is not available in the host "
                + "descriptor repository.");
        }

        return definition;
    }

    public async Task<EndpointDescriptor> ResolveAsync(
        CompactBootstrapResponse bootstrapResponse,
        CancellationToken cancellationToken = default)
    {
        EndpointDescriptorDefinition definition =
            await ResolveDefinitionAsync(
                bootstrapResponse,
                cancellationToken);

        return definition.Materialize(
            bootstrapResponse.EndpointId);
    }
}