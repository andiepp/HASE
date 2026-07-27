namespace Hase.Client;

/// <summary>
/// Identifies the normalized quality of one remote Property value.
/// </summary>
public enum RemotePropertyQuality
{
    /// <summary>
    /// No Property quality has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The Property value is good.
    /// </summary>
    Good = 1,

    /// <summary>
    /// The Property value is uncertain.
    /// </summary>
    Uncertain = 2,

    /// <summary>
    /// The Property value is bad.
    /// </summary>
    Bad = 3
}
