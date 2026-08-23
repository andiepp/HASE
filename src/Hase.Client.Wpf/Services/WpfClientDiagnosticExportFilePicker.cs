using Microsoft.Win32;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Presents the standard save dialog for one diagnostic export.
/// </summary>
public sealed class WpfClientDiagnosticExportFilePicker
    : IClientDiagnosticExportFilePicker
{
    public string? SelectExportTarget(string suggestedFileName)
    {
        var dialog =
            new SaveFileDialog
            {
                Title =
                    "Export diagnostics",
                FileName =
                    suggestedFileName,
                DefaultExt =
                    ".jsonl",
                Filter =
                    "HASE diagnostic export (*.jsonl)|*.jsonl"
                    + "|All files (*.*)|*.*",
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
