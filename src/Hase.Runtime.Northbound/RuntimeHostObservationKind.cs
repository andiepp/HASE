namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies the normalized kind of one northbound runtime-host observation.
/// </summary>
public enum RuntimeHostObservationKind
{
    /// <summary>
    /// A runtime endpoint attachment was published authoritatively.
    /// </summary>
    AttachmentPublished = 0,

    /// <summary>
    /// A published runtime endpoint attachment ended.
    /// </summary>
    AttachmentEnded = 1,

    /// <summary>
    /// The normalized connection status of a published attachment changed.
    /// </summary>
    ConnectionStatusChanged = 2,

    /// <summary>
    /// An authoritative runtime Property-cache value changed.
    /// </summary>
    PropertyValueChanged = 3,

    /// <summary>
    /// A transient runtime Event occurred.
    /// </summary>
    EventOccurred = 4
}