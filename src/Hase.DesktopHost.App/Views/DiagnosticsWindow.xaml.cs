using System.Windows;

namespace Hase.DesktopHost.App.Views;

public partial class DiagnosticsWindow
    : Window,
      IDesktopModelessWindow
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
    }

    bool IDesktopModelessWindow.IsMinimized =>
        WindowState == WindowState.Minimized;

    void IDesktopModelessWindow.Restore()
    {
        WindowState =
            WindowState.Normal;
    }

    void IDesktopModelessWindow.ShowWindow()
    {
        Show();
    }

    void IDesktopModelessWindow.ActivateWindow()
    {
        Activate();
    }
}
