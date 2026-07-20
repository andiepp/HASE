namespace Hase.CompactProtocol;

/// <summary>
/// Reports the outcome of one compact endpoint command execution.
/// </summary>
internal sealed record CompactExecuteCommandResponse
{
    public CompactExecuteCommandResponse(
        byte correlationId,
        byte commandId,
        CompactCommandExecutionStatus status)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact execute-command response must use a nonzero "
                + "correlation identifier.");
        }

        if (commandId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandId),
                commandId,
                "A compact execute-command response must use a nonzero "
                + "command identifier.");
        }

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The compact command execution status is not defined.");
        }

        CorrelationId =
            correlationId;

        CommandId =
            commandId;

        Status =
            status;
    }

    public byte CorrelationId
    {
        get;
    }

    public byte CommandId
    {
        get;
    }

    public CompactCommandExecutionStatus Status
    {
        get;
    }
}
