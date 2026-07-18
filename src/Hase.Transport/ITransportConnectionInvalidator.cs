namespace Hase.Transport;

/// <summary>
/// Provides an optional capability for marking a transport connection as
/// unusable after a higher-level health check has established that the remote
/// endpoint is no longer responsive.
/// </summary>
/// <remarks>
/// Invalidation does not perform reconnection. It changes the connection to
/// the existing faulted lifecycle state so that its owner can replace it
/// through the normal recovery path.
/// </remarks>
public interface ITransportConnectionInvalidator
{
    /// <summary>
    /// Marks the connection as faulted and unusable.
    /// </summary>
    void Invalidate();
}