namespace Hase.Client.Diagnostics;

/// <summary>
/// Identifies the stable client area that produced a diagnostic record.
/// </summary>
public enum ClientDiagnosticCategory
{
    ClientLifecycle = 0,
    ClientConfiguration = 1,
    ClientConnection = 2,
    ClientSnapshot = 3,
    ClientProperty = 4,
    ClientCommand = 5,
    ClientObservation = 6,
    ClientRecovery = 7,
    ClientPresentation = 8,
    NorthboundExchange = 9,
    NorthboundBytes = 10
}
