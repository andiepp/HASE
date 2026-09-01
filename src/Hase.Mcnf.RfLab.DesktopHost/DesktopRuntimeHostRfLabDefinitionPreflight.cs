using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.Mcnf.RfLab;
using Hase.Mcnf.RfLab.Hosting;

namespace Hase.Mcnf.RfLab.DesktopHost;

public static class DesktopRuntimeHostRfLabDefinitionPreflight
{
    public static async ValueTask<IReadOnlyList<DesktopRuntimeHostRfLabEndpointPlan>>
        ResolveAllAsync(
            IEnumerable<DesktopRuntimeHostRfLabSerialEndpointProfile> profiles,
            IEndpointDescriptorRepository definitionRepository,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(definitionRepository);
        cancellationToken.ThrowIfCancellationRequested();

        var plans = new List<DesktopRuntimeHostRfLabEndpointPlan>();

        foreach (DesktopRuntimeHostRfLabSerialEndpointProfile profile in profiles)
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

    public static async ValueTask<DesktopRuntimeHostRfLabEndpointPlan> ResolveAsync(
        DesktopRuntimeHostRfLabSerialEndpointProfile profile,
        IEndpointDescriptorRepository definitionRepository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(definitionRepository);
        cancellationToken.ThrowIfCancellationRequested();

        if (profile.DefinitionReference != RfLabReadOnlyDefinition.Reference
            && profile.DefinitionReference != RfLabControlledSignalDefinition.Reference
            && profile.DefinitionReference != RfLabPanelSignalDefinition.Reference)
        {
            throw new InvalidDataException(
                "The configured RF-Lab definition is not supported.");
        }

        EndpointDescriptorDefinition? definition = await definitionRepository
            .FindAsync(profile.DefinitionReference, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            throw new InvalidDataException(
                "The configured RF-Lab definition is unavailable.");
        }

        EndpointDescriptorDefinition expectedDefinition =
            profile.DefinitionReference == RfLabReadOnlyDefinition.Reference
                ? RfLabReadOnlyDefinition.EndpointDefinition
                : profile.DefinitionReference == RfLabControlledSignalDefinition.Reference
                    ? RfLabControlledSignalDefinition.EndpointDefinition
                    : RfLabPanelSignalDefinition.EndpointDefinition;
        if (!ReferenceEquals(definition, expectedDefinition))
        {
            throw new InvalidDataException(
                "The configured RF-Lab definition does not match its exact reference.");
        }

        return new DesktopRuntimeHostRfLabEndpointPlan(
            new EndpointId(profile.ExpectedEndpointId),
            definition);
    }
}

public sealed record DesktopRuntimeHostRfLabEndpointPlan
{
    public DesktopRuntimeHostRfLabEndpointPlan(
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
        $"RF-Lab endpoint plan '{ExpectedEndpointId.Value}'";
}
