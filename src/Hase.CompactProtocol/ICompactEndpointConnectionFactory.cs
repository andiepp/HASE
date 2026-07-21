using Hase.Core.Domain.Identity;
using Hase.Transport.Serial;

namespace Hase.CompactProtocol;

/// <summary>
/// Opens, bootstraps, validates, and resolves one compact serial endpoint
/// connection.
/// </summary>
internal interface ICompactEndpointConnectionFactory
{
    /// <summary>
    /// Opens and initializes one explicitly configured compact serial endpoint.
    /// </summary>
    Task<CompactEndpointConnection> ConnectAsync(
        SerialTransportOptions transportOptions,
        EndpointId? expectedEndpointId,
        CancellationToken cancellationToken = default);
}