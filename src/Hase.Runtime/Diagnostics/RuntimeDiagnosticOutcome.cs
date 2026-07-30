namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Identifies the stable outcome of a completed diagnostic activity.
/// </summary>
public enum RuntimeDiagnosticOutcome
{
    Succeeded = 0,
    Failed = 1,
    Cancelled = 2,
    TimedOut = 3
}
