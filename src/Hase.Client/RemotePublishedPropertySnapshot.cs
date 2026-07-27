using Hase.Core.Domain.Properties;

namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized snapshot of a remote runtime-host
/// Property cache entry.
/// </summary>
public sealed record RemotePublishedPropertySnapshot
{
    /// <summary>
    /// Initializes one normalized published Property snapshot.
    /// </summary>
    public RemotePublishedPropertySnapshot(
        RemotePropertyTarget target,
        PropertyDescriptor descriptor,
        RemoteEndpointConnectionStatus connectionStatus,
        RemotePropertyValue? currentValue)
    {
        Target =
            target
            ?? throw new ArgumentNullException(
                nameof(target));

        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));

        ConnectionStatus =
            connectionStatus
            ?? throw new ArgumentNullException(
                nameof(connectionStatus));

        if (target.PropertyId
            != descriptor.Id)
        {
            throw new ArgumentException(
                "The Property descriptor identity must match the remote "
                + "target.",
                nameof(descriptor));
        }

        CurrentValue =
            currentValue;
    }

    /// <summary>
    /// Gets the generation-scoped remote Property target.
    /// </summary>
    public RemotePropertyTarget Target
    {
        get;
    }

    /// <summary>
    /// Gets the immutable Property descriptor.
    /// </summary>
    public PropertyDescriptor Descriptor
    {
        get;
    }

    /// <summary>
    /// Gets the captured physical endpoint connection status.
    /// </summary>
    public RemoteEndpointConnectionStatus ConnectionStatus
    {
        get;
    }

    /// <summary>
    /// Gets the current cached Property value when known.
    /// </summary>
    public RemotePropertyValue? CurrentValue
    {
        get;
    }

    /// <summary>
    /// Gets whether the runtime host currently supplies a cached Property
    /// value record.
    /// </summary>
    public bool IsKnown =>
        CurrentValue is not null;
}
