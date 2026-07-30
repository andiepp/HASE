using Hase.Core.Domain.Data;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Properties;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeEventViewModel
{
    public DesktopRuntimeEventViewModel(
        DesktopRuntimeEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        Path =
            string.IsNullOrWhiteSpace(snapshot.Path)
                ? throw new ArgumentException(
                    "The Event path must not be empty.",
                    nameof(snapshot))
                : snapshot.Path;
        DisplayName =
            string.IsNullOrWhiteSpace(snapshot.DisplayName)
                ? throw new ArgumentException(
                    "The Event display name must not be empty.",
                    nameof(snapshot))
                : snapshot.DisplayName;
        Description =
            snapshot.Description
            ?? string.Empty;
        Descriptor =
            snapshot.Descriptor
            ?? new EventDescriptor(
                DescriptorPath.Parse(
                    Path),
                DisplayName)
            {
                Description =
                    snapshot.Description
            };
        HasPayload =
            Descriptor.Payload
            is not null;
        PayloadDisplayName =
            Descriptor.Payload?.DisplayName
            ?? "None";
        PayloadDescription =
            Descriptor.Payload?.Description
            ?? string.Empty;
        PayloadDataKind =
            Descriptor.Payload?.Data switch
            {
                BooleanDataDescriptor =>
                    "Boolean",
                NumericDataDescriptor =>
                    "Numeric",
                StringDataDescriptor =>
                    "String",
                ByteArrayDataDescriptor =>
                    "ByteArray",
                null =>
                    "None",
                DataDescriptor data =>
                    data.GetType().Name
            };
    }

    public string Path
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string Description
    {
        get;
    }

    public bool HasPayload { get; }

    public string PayloadDisplayName { get; }

    public string PayloadDescription { get; }

    public string PayloadDataKind { get; }

    public Hase.Core.Domain.Events.EventDescriptor Descriptor { get; }

    public bool HasSameDescriptor(
        DesktopRuntimeEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        return string.Equals(
                Path,
                snapshot.Path,
                StringComparison.Ordinal)
            && string.Equals(
                DisplayName,
                snapshot.DisplayName,
                StringComparison.Ordinal)
            && string.Equals(
                Description,
                snapshot.Description
                    ?? string.Empty,
                StringComparison.Ordinal)
            && Equals(
                Descriptor,
                snapshot.Descriptor
                ?? new EventDescriptor(
                    DescriptorPath.Parse(
                        snapshot.Path),
                    snapshot.DisplayName)
                {
                    Description =
                        snapshot.Description
                });
    }
}
