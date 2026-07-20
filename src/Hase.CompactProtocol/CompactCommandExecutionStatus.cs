namespace Hase.CompactProtocol;

/// <summary>
/// Reports the endpoint-side outcome of one compact command execution.
/// </summary>
internal enum CompactCommandExecutionStatus : byte
{
    Success =
        0x00,

    UnknownCommand =
        0x01,

    ExecutionFailed =
        0x02
}