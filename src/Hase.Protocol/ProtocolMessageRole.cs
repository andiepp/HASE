namespace Hase.Protocol;

/// <summary>
/// Identifies the role of a protocol message within a communication
/// exchange.
/// </summary>
public enum ProtocolMessageRole : byte
{
    /// <summary>
    /// Initiates an operation and may require a corresponding response.
    /// </summary>
    Request = 1,

    /// <summary>
    /// Completes or reports the result of a previous request.
    /// </summary>
    Response = 2,

    /// <summary>
    /// Reports information without being a direct response to a request.
    /// </summary>
    Notification = 3
}