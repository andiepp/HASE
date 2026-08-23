namespace Hase.DesktopHost.App.ViewModels;

/// <summary>
/// Lets the operator choose the target file of one diagnostic export.
/// </summary>
public interface IDesktopDiagnosticExportDialogService
{
    /// <summary>
    /// Returns the chosen fully qualified target path, or null when the
    /// operator cancels the export.
    /// </summary>
    string? SelectExportTarget(
        string suggestedFileName);
}
