using Hase.Client.Wpf.RfLab;
using Hase.Client.Wpf.RfLab.Presets;
using Hase.Client.Wpf.Services;

namespace Hase.Client.Wpf.AppHost.Lab;

/// <summary>
/// The Client with this laboratory's instrument panels composed into it.
/// </summary>
/// <remarks>
/// This is the add-on's own entry point. The published Client ships no
/// panel; this one composes the RF-Lab operating surface and names where
/// its stored settings live, because a path that exists on one computer
/// need not exist on another.
/// </remarks>
public sealed class LabApp : global::Hase.Client.Wpf.AppHost.App
{
    /// <summary>
    /// Composes the instrument panels this laboratory's Client ships.
    /// </summary>
    public static IEnumerable<IClientInstrumentPanel> CreateLabInstrumentPanels() =>
        [
            new RfLabInstrumentPanel(
                new RfLabPresetDirectoryStore(
                    RfLabPresetDirectoryStore.DefaultDirectoryPath))
        ];

    protected override IEnumerable<IClientInstrumentPanel>
        CreateInstrumentPanels() =>
        CreateLabInstrumentPanels();

    /// <summary>
    /// This application's entry point, deliberately replacing the one the
    /// published application declares.
    /// </summary>
    [STAThread]
    public static new void Main() =>
        new LabApp().Run();
}
