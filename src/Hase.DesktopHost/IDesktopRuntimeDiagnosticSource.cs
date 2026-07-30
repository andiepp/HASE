using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Exposes the current process-local Desktop Runtime Host diagnostic session.
/// </summary>
public interface IDesktopRuntimeDiagnosticSource
{
    /// <summary>
    /// Captures retained records in process-local sequence order.
    /// </summary>
    IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics();

    /// <summary>
    /// Removes all records retained by the current diagnostic session.
    /// </summary>
    void ClearDiagnostics();
}
