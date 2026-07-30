using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimeCommandSnapshot(
    RuntimeHostCommandTarget Target,
    string Path,
    string DisplayName,
    string? Description,
    bool IsEndpointReady,
    CommandDescriptor Descriptor)
{
    public DesktopRuntimeCommandSnapshot(
        RuntimeHostCommandTarget target,
        string path,
        string displayName,
        string? description,
        bool isEndpointReady)
        : this(
            target,
            path,
            displayName,
            description,
            isEndpointReady,
            new CommandDescriptor(
                DescriptorPath.Parse(
                    path),
                displayName)
            {
                Description =
                    description
            })
    {
    }
}
