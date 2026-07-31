using System.Windows;
using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.App.Views;

public sealed class WpfDesktopDiagnosticsWindowFactory
    : IDesktopDiagnosticsWindowFactory
{
    public IDesktopModelessWindow Create(
        RuntimeDiagnosticsViewModel diagnosticsViewModel)
    {
        ArgumentNullException.ThrowIfNull(
            diagnosticsViewModel);

        return new DiagnosticsWindow
        {
            DataContext =
                diagnosticsViewModel,
            Owner =
                Application.Current?.MainWindow
        };
    }
}
