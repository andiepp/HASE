namespace Hase.Transport;

/// <summary>
/// Describes the result of one transport request/response exchange.
/// </summary>
public enum TransportExchangeOutcome
{
    /// <summary>
    /// The request was sent and a complete response was received.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The exchange failed because of a transport or processing error.
    /// </summary>
    Failed,

    /// <summary>
    /// The exchange was cancelled.
    /// </summary>
    Cancelled
}