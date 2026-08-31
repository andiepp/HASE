#nullable enable

using System.Windows;
using Hase.Client.Wpf.RfLab.ViewModels;
using Hase.Client.Wpf.RfLab.Views;
using Hase.Client.Wpf.Services;
using Hase.Mcnf.RfLab;

namespace Hase.Client.Wpf.RfLab;

/// <summary>
/// Hosts the RF-Lab operating surface as a detached panel of the client
/// workspace, following the established detached-window pattern.
/// </summary>
public sealed class RfLabInstrumentPanel : IClientInstrumentPanel
{
    private RfLabPanelWindow? window;

    /// <inheritdoc />
    public string PanelId => RfLabPanelDeclaration.PanelId;

    /// <inheritdoc />
    public void Open(ClientInstrumentPanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            return;
        }

        window = new RfLabPanelWindow(new RfLabPanelViewModel(context))
        {
            Owner = Application.Current?.MainWindow
        };
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    /// <inheritdoc />
    public void Close()
    {
        RfLabPanelWindow? active = window;
        window = null;
        active?.Close();
    }
}
