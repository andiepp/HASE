namespace Hase.Protocol;

/// <summary>
/// Reports the result of executing a command.
/// </summary>
public sealed record ExecuteCommandResponse(
    CorrelationId CorrelationId,
    ProtocolResult Result,
    object? ReturnValue)
    : ProtocolResultResponse(
        ProtocolVersion.Current,
        ProtocolMessageType.ExecuteCommandResponse,
        CorrelationId,
        Result);