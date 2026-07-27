using Microsoft.Win32;

namespace Hase.Client.Wpf.Services;

public sealed class WpfClientConfigurationFilePicker
    : IClientConfigurationFilePicker
{
    public string? PickConfigurationFile()
    {
        var dialog =
            new OpenFileDialog
            {
                CheckFileExists =
                    true,
                CheckPathExists =
                    true,
                Filter =
                    "HASE client configuration (*.json)|*.json"
                    + "|All files (*.*)|*.*",
                Multiselect =
                    false,
                Title =
                    "Select HASE client configuration"
            };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}
