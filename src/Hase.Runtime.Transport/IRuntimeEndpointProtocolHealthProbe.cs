using Hase.Protocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Probes the health of the active endpoint connection through its current
/// runtime protocol binding.
/// </summary>
public interface IRuntimeEndpointProtocolHealthProbe
{
    /// <summary>
    /// Sends one protocol request through the active binding and requires its
    /// correlated response within the supplied timeout.
    /// </summary>
    /// <param name="request">
    /// Protocol request used as the health probe.
    /// </param>
    /// <param name="timeout">
    /// Maximum duration allowed for the complete protocol exchange.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the probe independently of its timeout.
    /// </param>
    /// <returns>
    /// The correlated protocol response.
    /// </returns>
    /// <exception cref="TimeoutException">
    /// The endpoint did not complete the probe within the supplied timeout.
    /// </exception>
    Task<ProtocolMessage> ProbeAsync(
        ProtocolMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}