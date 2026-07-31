using System.Windows;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Client.Wpf.Views;

namespace Hase.Client.Wpf.AppHost;

public sealed class ClientDiagnosticsWindowController : IClientDiagnosticsWindowController
{
    private readonly ClientDiagnosticsViewModel viewModel;
    private DiagnosticsWindow? window;

    public ClientDiagnosticsWindowController(ClientDiagnosticsViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public void Open()
    {
        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Activate();
            return;
        }

        window = new DiagnosticsWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow
        };
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public void Close()
    {
        DiagnosticsWindow? active = window;
        window = null;
        active?.Close();
    }
}
