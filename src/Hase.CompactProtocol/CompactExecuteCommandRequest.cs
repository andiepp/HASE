namespace Hase.CompactProtocol;

/// <summary>
/// Requests execution of one compact endpoint command identified by its
/// resource-constrained wire identifier.
/// </summary>
internal sealed record CompactExecuteCommandRequest
{
    public CompactExecuteCommandRequest(
        byte correlationId,
        byte commandId)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact execute-command request must use a nonzero "
                + "correlation identifier.");
        }

        if (commandId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandId),
                commandId,
                "A compact execute-command request must use a nonzero "
                + "command identifier.");
        }

        CorrelationId =
            correlationId;

        CommandId =
            commandId;
    }

    public byte CorrelationId
    {
        get;
    }

    public byte CommandId
    {
        get;
    }
}