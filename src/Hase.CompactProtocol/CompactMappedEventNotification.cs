namespace Hase.CompactProtocol;

/// <summary>
/// Represents one decoded compact event notification after authoritative
/// descriptor-side event mapping has been resolved.
/// </summary>
internal sealed record CompactMappedEventNotification
{
    public CompactMappedEventNotification(
        CompactEventNotification notification,
        CompactEventMapping mapping)
    {
        Notification =
            notification
            ?? throw new ArgumentNullException(
                nameof(notification));

        Mapping =
            mapping
            ?? throw new ArgumentNullException(
                nameof(mapping));
    }

    /// <summary>
    /// Gets the decoded wire notification.
    /// </summary>
    public CompactEventNotification Notification
    {
        get;
    }

    /// <summary>
    /// Gets the authoritative host-side event mapping.
    /// </summary>
    public CompactEventMapping Mapping
    {
        get;
    }
}