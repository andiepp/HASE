namespace Hase.Client.Wpf.Services;

/// <summary>
/// Lets the operator choose the target file of one diagnostic export.
/// </summary>
public interface IClientDiagnosticExportFilePicker
{
    /// <summary>
    /// Returns the chosen fully qualified target path, or null when the
    /// operator cancels the export.
    /// </summary>
    string? SelectExportTarget(string suggestedFileName);
}
