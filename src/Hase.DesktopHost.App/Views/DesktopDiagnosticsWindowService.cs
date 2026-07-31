using System.Windows;
using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.App.Views;

public sealed class DesktopDiagnosticsWindowService
    : IDesktopDiagnosticsWindowService
{
    private readonly SingleInstanceDesktopWindowController controller;

    public DesktopDiagnosticsWindowService(
        RuntimeDiagnosticsViewModel diagnosticsViewModel)
    {
        ArgumentNullException.ThrowIfNull(
            diagnosticsViewModel);

        controller =
            new SingleInstanceDesktopWindowController(
                () =>
                    new DiagnosticsWindow
                    {
                        DataContext =
                            diagnosticsViewModel,
                        Owner =
                            Application.Current?.MainWindow
                    });
    }

    public void Open()
    {
        controller.Open();
    }
}
