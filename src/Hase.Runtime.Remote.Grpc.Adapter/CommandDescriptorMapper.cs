using DomainCommands = global::Hase.Core.Domain.Commands;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps Command descriptors to the version 1 remote contract.
/// </summary>
public sealed class CommandDescriptorMapper
    : ICommandDescriptorMapper
{
    private readonly IDataDescriptorMapper dataDescriptorMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public CommandDescriptorMapper(
        IDataDescriptorMapper dataDescriptorMapper)
    {
        this.dataDescriptorMapper =
            dataDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(dataDescriptorMapper));
    }

    /// <inheritdoc />
    public GrpcV1.CommandDescriptor Map(
        DomainCommands.CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        var result =
            new GrpcV1.CommandDescriptor
            {
                DisplayName =
                    descriptor.DisplayName
            };

        result.PathSegments.Add(
            descriptor.Path.Segments);

        if (descriptor.Description is not null)
        {
            result.Description =
                descriptor.Description;
        }

        if (descriptor.Argument is not null)
        {
            result.Argument =
                MapArgument(
                    descriptor.Argument);
        }

        if (descriptor.Presentation is not null)
        {
            result.Presentation =
                MapPresentation(
                    descriptor.Presentation);
        }

        result.RequiresExplicitConfirmation =
            descriptor.RequiresExplicitConfirmation;

        return result;
    }

    private static GrpcV1.CommandPresentation MapPresentation(
        DomainCommands.CommandPresentation presentation)
    {
        var result =
            new GrpcV1.CommandPresentation();

        if (presentation.ShortLabel is not null)
        {
            result.ShortLabel =
                presentation.ShortLabel;
        }

        if (presentation.SelectionGroupId is not null)
        {
            result.SelectionGroupId =
                presentation.SelectionGroupId;
        }

        if (presentation.SelectionStatePath is not null)
        {
            result.SelectionStatePathSegments.Add(
                presentation.SelectionStatePath.Segments);
        }

        if (presentation.SelectionValue is not null)
        {
            result.SelectionValue =
                presentation.SelectionValue;
        }

        return result;
    }

    private GrpcV1.CommandArgumentDescriptor MapArgument(
        DomainCommands.CommandArgumentDescriptor descriptor)
    {
        var result =
            new GrpcV1.CommandArgumentDescriptor
            {
                DisplayName =
                    descriptor.DisplayName,
                Data =
                    dataDescriptorMapper.Map(
                        descriptor.Data)
                    ?? throw new InvalidOperationException(
                        "The data descriptor mapper returned null.")
            };

        if (descriptor.Description is not null)
        {
            result.Description =
                descriptor.Description;
        }

        return result;
    }
}
