using Hase.DesktopHost.App.ViewModels;
using Microsoft.Win32;

namespace Hase.DesktopHost.App.Views;

/// <summary>
/// Presents the standard save dialog for one diagnostic export.
/// </summary>
public sealed class WpfDesktopDiagnosticExportDialogService
    : IDesktopDiagnosticExportDialogService
{
    public string? SelectExportTarget(
        string suggestedFileName)
    {
        SaveFileDialog dialog =
            new()
            {
                Title =
                    "Export diagnostics",
                FileName =
                    suggestedFileName,
                DefaultExt =
                    ".jsonl",
                Filter =
                    "HASE diagnostic export (*.jsonl)|*.jsonl|All files (*.*)|*.*",
                AddExtension =
                    true,
                OverwritePrompt =
                    true
            };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}
