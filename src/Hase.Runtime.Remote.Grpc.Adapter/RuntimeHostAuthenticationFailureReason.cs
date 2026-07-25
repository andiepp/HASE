namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies a deterministic transport-independent authentication failure.
/// </summary>
public enum RuntimeHostAuthenticationFailureReason
{
    /// <summary>
    /// No failure has occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// The validated credential identity is not enrolled.
    /// </summary>
    UnknownCredential = 1
}
