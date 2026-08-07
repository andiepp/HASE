namespace Hase.Client;

/// <summary>Identifies the optional outcome of a remote diagnostic operation.</summary>
public enum RemoteRuntimeDiagnosticOutcome
{
    Succeeded = 0,
    Failed = 1,
    Cancelled = 2,
    TimedOut = 3
}
