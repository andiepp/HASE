using DomainCommands = global::Hase.Core.Domain.Commands;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps Command descriptors to the version 1 remote contract.
/// </summary>
public sealed class CommandDescriptorMapper
    : ICommandDescriptorMapper
{
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

        return result;
    }
}
