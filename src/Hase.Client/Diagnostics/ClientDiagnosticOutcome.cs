namespace Hase.Client.Diagnostics;

/// <summary>
/// Identifies the stable outcome of a completed client activity.
/// </summary>
public enum ClientDiagnosticOutcome
{
    Succeeded = 0,
    Failed = 1,
    Cancelled = 2,
    TimedOut = 3
}
