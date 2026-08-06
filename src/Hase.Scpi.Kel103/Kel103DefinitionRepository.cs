using Hase.Core.Domain.Descriptors;

namespace Hase.Scpi.Kel103;

public sealed class Kel103DefinitionRepository : IEndpointDescriptorRepository
{
    private readonly InMemoryEndpointDescriptorRepository repository = new(
    [
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            Kel103IdentityDefinition.Reference,
            Kel103IdentityDefinition.EndpointDefinition),
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            Kel103ReadOnlyMeasurementDefinition.Reference,
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition),
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            Kel103OperatingStateDefinition.Reference,
            Kel103OperatingStateDefinition.EndpointDefinition),
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            Kel103ControlledSetpointDefinition.Reference,
            Kel103ControlledSetpointDefinition.EndpointDefinition),
        new KeyValuePair<DescriptorReference, EndpointDescriptorDefinition>(
            Kel103ControlledInputDefinition.Reference,
            Kel103ControlledInputDefinition.EndpointDefinition)
    ]);

    public ValueTask<EndpointDescriptorDefinition?> FindAsync(
        DescriptorReference reference,
        CancellationToken cancellationToken = default) =>
        repository.FindAsync(reference, cancellationToken);
}
