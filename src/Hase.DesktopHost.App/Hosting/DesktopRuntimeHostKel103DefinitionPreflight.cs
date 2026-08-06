using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.Scpi.Kel103;

namespace Hase.DesktopHost.App.Hosting;

public static class DesktopRuntimeHostKel103DefinitionPreflight
{
    public static async ValueTask<IReadOnlyList<DesktopRuntimeHostKel103EndpointPlan>>
        ResolveAllAsync(
            IEnumerable<DesktopRuntimeHostKel103SerialEndpointProfile> profiles,
            IEndpointDescriptorRepository definitionRepository,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(definitionRepository);
        cancellationToken.ThrowIfCancellationRequested();

        var plans = new List<DesktopRuntimeHostKel103EndpointPlan>();

        foreach (DesktopRuntimeHostKel103SerialEndpointProfile profile in profiles)
        {
            plans.Add(
                await ResolveAsync(
                        profile,
                        definitionRepository,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        return plans.AsReadOnly();
    }

    public static async ValueTask<DesktopRuntimeHostKel103EndpointPlan> ResolveAsync(
        DesktopRuntimeHostKel103SerialEndpointProfile profile,
        IEndpointDescriptorRepository definitionRepository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(definitionRepository);
        cancellationToken.ThrowIfCancellationRequested();

        if (profile.DefinitionReference != Kel103ReadOnlyMeasurementDefinition.Reference
            && profile.DefinitionReference != Kel103OperatingStateDefinition.Reference
            && profile.DefinitionReference != Kel103ControlledSetpointDefinition.Reference
            && profile.DefinitionReference != Kel103ControlledInputDefinition.Reference)
        {
            throw new InvalidDataException(
                "The configured KEL-103 definition is not supported.");
        }

        EndpointDescriptorDefinition? definition = await definitionRepository
            .FindAsync(profile.DefinitionReference, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            throw new InvalidDataException(
                "The configured KEL-103 definition is unavailable.");
        }

        EndpointDescriptorDefinition expectedDefinition =
            profile.DefinitionReference == Kel103ReadOnlyMeasurementDefinition.Reference
                ? Kel103ReadOnlyMeasurementDefinition.EndpointDefinition
                : profile.DefinitionReference == Kel103OperatingStateDefinition.Reference
                    ? Kel103OperatingStateDefinition.EndpointDefinition
                    : profile.DefinitionReference == Kel103ControlledSetpointDefinition.Reference
                        ? Kel103ControlledSetpointDefinition.EndpointDefinition
                        : Kel103ControlledInputDefinition.EndpointDefinition;
        if (!ReferenceEquals(definition, expectedDefinition))
        {
            throw new InvalidDataException(
                "The configured KEL-103 definition does not match its exact reference.");
        }

        return new DesktopRuntimeHostKel103EndpointPlan(
            new EndpointId(profile.ExpectedEndpointId),
            definition);
    }
}

public sealed record DesktopRuntimeHostKel103EndpointPlan
{
    public DesktopRuntimeHostKel103EndpointPlan(
        EndpointId expectedEndpointId,
        EndpointDescriptorDefinition definition)
    {
        ExpectedEndpointId = expectedEndpointId
            ?? throw new ArgumentNullException(nameof(expectedEndpointId));
        Definition = definition
            ?? throw new ArgumentNullException(nameof(definition));
    }

    public EndpointId ExpectedEndpointId { get; }
    public EndpointDescriptorDefinition Definition { get; }

    public override string ToString() =>
        $"KEL-103 endpoint plan '{ExpectedEndpointId.Value}'";
}
