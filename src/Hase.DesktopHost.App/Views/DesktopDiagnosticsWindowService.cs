using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.App.Views;

public sealed class DesktopDiagnosticsWindowService
    : IDesktopDiagnosticsWindowService
{
    private readonly SingleInstanceDesktopWindowController controller;

    public DesktopDiagnosticsWindowService(
        RuntimeDiagnosticsViewModel diagnosticsViewModel,
        IDesktopDiagnosticsWindowFactory windowFactory)
    {
        ArgumentNullException.ThrowIfNull(
            diagnosticsViewModel);
        ArgumentNullException.ThrowIfNull(
            windowFactory);

        controller =
            new SingleInstanceDesktopWindowController(
                () =>
                    windowFactory.Create(
                        diagnosticsViewModel));
    }

    public void Open()
    {
        controller.Open();
    }
}
