namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Identifies one semantic version 1 northbound remote operation independently
/// from transport method-name strings.
/// </summary>
public enum RuntimeHostRemoteOperation
{
    /// <summary>
    /// No operation has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Retrieve the immutable runtime-host snapshot.
    /// </summary>
    GetSnapshot = 1,

    /// <summary>
    /// Read one Property value from the runtime cache.
    /// </summary>
    ReadCachedProperty = 2,

    /// <summary>
    /// Read one Property value authoritatively from the endpoint.
    /// </summary>
    ReadAuthoritativeProperty = 3,

    /// <summary>
    /// Write one Property and await endpoint confirmation.
    /// </summary>
    WriteProperty = 4,

    /// <summary>
    /// Execute one Command exactly once.
    /// </summary>
    ExecuteCommand = 5,

    /// <summary>
    /// Open one live-observation subscription.
    /// </summary>
    Observe = 6,

    /// <summary>
    /// Open one live diagnostic projection subscription.
    /// </summary>
    ObserveDiagnostics = 7
}
