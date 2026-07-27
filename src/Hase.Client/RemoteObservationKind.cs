namespace Hase.Client;

/// <summary>
/// Identifies one normalized remote runtime-host observation kind.
/// </summary>
public enum RemoteObservationKind
{
    /// <summary>
    /// No remote observation kind has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A runtime endpoint attachment was published.
    /// </summary>
    AttachmentPublished = 1,

    /// <summary>
    /// A published runtime endpoint attachment ended.
    /// </summary>
    AttachmentEnded = 2,

    /// <summary>
    /// A published attachment's physical connection status changed.
    /// </summary>
    ConnectionStatusChanged = 3,

    /// <summary>
    /// An authoritative runtime Property-cache value changed.
    /// </summary>
    PropertyValueChanged = 4,

    /// <summary>
    /// A transient runtime Event occurred.
    /// </summary>
    EventOccurred = 5
}
