using Hase.Core.Domain.Events;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimeEventSnapshot(
    string Path,
    string DisplayName,
    string? Description,
    EventDescriptor? Descriptor = null)
{
    public DesktopRuntimeEventSnapshot(
        EventDescriptor descriptor)
        : this(
            (descriptor
                ?? throw new ArgumentNullException(
                    nameof(descriptor)))
                .Path.ToString(),
            descriptor.DisplayName,
            descriptor.Description,
            descriptor)
    {
    }
}
