namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Describes why a discovered network candidate could not be verified
/// as a HASE endpoint.
/// </summary>
public enum NetworkEndpointVerificationFailure
{
    /// <summary>
    /// The candidate could not be reached through its advertised
    /// address and port.
    /// </summary>
    Unreachable = 0,

    /// <summary>
    /// Candidate verification did not complete within the configured
    /// timeout.
    /// </summary>
    TimedOut = 1,

    /// <summary>
    /// The reachable peer did not behave as a HASE Protocol Version 1
    /// endpoint.
    /// </summary>
    NonHaseEndpoint = 2,

    /// <summary>
    /// The peer returned a HASE response that was malformed,
    /// unexpected, or otherwise invalid for candidate verification.
    /// </summary>
    InvalidProtocolResponse = 3
}