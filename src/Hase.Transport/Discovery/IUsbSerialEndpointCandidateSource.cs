namespace Hase.Transport.Discovery;

/// <summary>
/// Enumerates operating-system serial connection targets that may
/// expose HASE compact endpoints.
/// </summary>
/// <remarks>
/// Enumerated candidates are not verified HASE endpoints.
/// Every selected candidate must be verified separately through
/// Compact Serial Protocol bootstrap.
/// </remarks>
public interface IUsbSerialEndpointCandidateSource
{
    /// <summary>
    /// Enumerates the currently available USB serial candidates.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stops enumeration and candidate delivery.
    /// </param>
    /// <returns>
    /// An asynchronous stream of serial candidates.
    /// </returns>
    IAsyncEnumerable<UsbSerialEndpointCandidate> EnumerateAsync(
        CancellationToken cancellationToken = default);
}