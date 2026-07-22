using Hase.CompactProtocol;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Resolves and validates the compact endpoint definition used to create
/// operational attachment resources after temporary authoritative bootstrap.
/// </summary>
internal sealed class CompactEndpointOperationalDefinitionResolver
{
    private readonly ICompactEndpointDefinitionRepository _repository;
    private readonly EndpointDescriptorCompatibilityValidator
        _compatibilityValidator;

    public CompactEndpointOperationalDefinitionResolver(
        ICompactEndpointDefinitionRepository repository)
        : this(
            repository,
            new EndpointDescriptorCompatibilityValidator())
    {
    }

    internal CompactEndpointOperationalDefinitionResolver(
        ICompactEndpointDefinitionRepository repository,
        EndpointDescriptorCompatibilityValidator compatibilityValidator)
    {
        _repository =
            repository
            ?? throw new ArgumentNullException(
                nameof(repository));

        _compatibilityValidator =
            compatibilityValidator
            ?? throw new ArgumentNullException(
                nameof(compatibilityValidator));
    }

    public async Task<CompactEndpointDefinition> ResolveAsync(
        CompactEndpointAttachmentBootstrapResult bootstrapResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            bootstrapResult);

        cancellationToken.ThrowIfCancellationRequested();

        CompactEndpointDefinition? definition =
            await _repository.FindAsync(
                bootstrapResult.DescriptorReference,
                cancellationToken);

        if (definition is null)
        {
            throw new CompactDescriptorNotFoundException(
                bootstrapResult.DescriptorReference);
        }

        if (definition.DescriptorReference
            != bootstrapResult.DescriptorReference)
        {
            throw new InvalidDataException(
                $"The compact endpoint-definition repository returned "
                + $"'{definition.DescriptorReference.Id.Value}' version "
                + $"{definition.DescriptorReference.Version} for requested "
                + $"reference "
                + $"'{bootstrapResult.DescriptorReference.Id.Value}' version "
                + $"{bootstrapResult.DescriptorReference.Version}.");
        }

        _compatibilityValidator.Validate(
            bootstrapResult.Descriptor,
            definition.DescriptorDefinition.Materialize(
                bootstrapResult.EndpointId));

        return definition;
    }
}