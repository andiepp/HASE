using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one immutable snapshot of a runtime-host Property cache entry.
/// </summary>
public sealed record PublishedRuntimePropertySnapshot
{
    /// <summary>
    /// Initializes a published runtime Property snapshot.
    /// </summary>
    public PublishedRuntimePropertySnapshot(
        RuntimeHostPropertyTarget target,
        PropertyDescriptor descriptor,
        EndpointConnectionStatus connectionStatus,
        PropertyValue? currentValue)
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
                "The Property descriptor identity must match the target.",
                nameof(descriptor));
        }

        CurrentValue =
            currentValue;
    }

    /// <summary>
    /// Gets the generation-scoped Property target.
    /// </summary>
    public RuntimeHostPropertyTarget Target
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
    /// Gets the captured endpoint connection status.
    /// </summary>
    public EndpointConnectionStatus ConnectionStatus
    {
        get;
    }

    /// <summary>
    /// Gets the current cached Property value, when known.
    /// </summary>
    public PropertyValue? CurrentValue
    {
        get;
    }

    /// <summary>
    /// Gets whether the runtime host currently knows an authoritative cached
    /// value.
    /// </summary>
    public bool IsKnown =>
        CurrentValue is not null;
}