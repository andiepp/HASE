using Hase.Core.Domain.Descriptors;

namespace Hase.Mcnf.RfLab;

public sealed class RfLabDefinitionRepository : IEndpointDescriptorRepository
{
    private readonly InMemoryEndpointDescriptorRepository repository = new(
    [
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            RfLabReadOnlyDefinition.Reference,
            RfLabReadOnlyDefinition.EndpointDefinition),
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            RfLabControlledSignalDefinition.Reference,
            RfLabControlledSignalDefinition.EndpointDefinition),
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            RfLabPanelSignalDefinition.Reference,
            RfLabPanelSignalDefinition.EndpointDefinition)
    ]);

    public ValueTask<EndpointDescriptorDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default) =>
        repository.FindAsync(reference, cancellationToken);
}
