using Hase.Protocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Sends HASE protocol requests and returns their correlated protocol
/// responses.
/// </summary>
/// <remarks>
/// This abstraction hides whether the underlying transport uses a legacy
/// request/response exchange or a duplex protocol session.
///
/// Implementations must accept request-role messages only and must return the
/// response whose correlation identifier matches the request.
/// </remarks>
public interface IRuntimeProtocolConnection
{
    /// <summary>
    /// Sends one protocol request and waits for its correlated response.
    /// </summary>
    /// <param name="request">
    /// The request-role protocol message to send.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the protocol operation.
    /// </param>
    /// <returns>
    /// The correlated protocol response.
    /// </returns>
    Task<ProtocolMessage> SendAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken = default);
}