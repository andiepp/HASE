using Hase.DesktopHost.App.ViewModels;

namespace Hase.DesktopHost.App.Views;

public interface IDesktopDiagnosticsWindowFactory
{
    IDesktopModelessWindow Create(
        RuntimeDiagnosticsViewModel diagnosticsViewModel);
}
