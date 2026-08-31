#nullable enable

using System.Windows;
using Hase.Client.Wpf.RfLab.Presets;
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
    private readonly IRfLabPresetStore presetStore;

    private RfLabPanelWindow? window;

    /// <param name="presetStore">
    /// Supplies the panel's stored settings. The composing application names
    /// the location, because a path that exists on one computer need not
    /// exist on another. When none is given the client's own preset
    /// directory is used, which is empty until an operator puts files there.
    /// </param>
    public RfLabInstrumentPanel(IRfLabPresetStore? presetStore = null) =>
        this.presetStore = presetStore
            ?? new RfLabPresetDirectoryStore(
                RfLabPresetDirectoryStore.DefaultDirectoryPath);

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

        window = new RfLabPanelWindow(
            new RfLabPanelViewModel(context, presetStore: presetStore))
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
