namespace Hase.Client.Diagnostics;

/// <summary>
/// Identifies the cumulative amount of client diagnostic detail requested.
/// </summary>
public enum ClientDiagnosticLevel
{
    Operational = 0,
    Protocol = 1,
    Bytes = 2
}
