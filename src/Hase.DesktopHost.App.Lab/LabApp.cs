using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Hosting;
using Hase.Mcnf.RfLab.DesktopHost;
using Hase.Scpi.Kel103.DesktopHost;

namespace Hase.DesktopHost.App.Lab;

/// <summary>
/// The Runtime Host with this laboratory's instruments composed into it.
/// </summary>
/// <remarks>
/// This is the add-on's own entry point. The published application ships
/// the generic endpoint kinds and names no instrument; this one registers
/// the KEL-103 and RF-Lab providers alongside them and changes nothing
/// else.
/// </remarks>
public sealed class LabApp : global::Hase.DesktopHost.App.App
{
    /// <summary>
    /// Composes the generic endpoint kinds this laboratory's Runtime Host
    /// ships alongside its own instruments.
    /// </summary>
    public static DesktopRuntimeHostEndpointProviderRegistry
        CreateLabEndpointProviders() =>
        new(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProvider(),
                new DesktopRuntimeHostCompactSerialEndpointProvider(),
                new DesktopRuntimeHostKel103EndpointProvider(),
                new DesktopRuntimeHostRfLabEndpointProvider()
            ]);

    protected override DesktopRuntimeHostEndpointProviderRegistry
        CreateEndpointProviders() =>
        CreateLabEndpointProviders();

    /// <summary>
    /// This application's entry point, deliberately replacing the one the
    /// published application declares.
    /// </summary>
    [STAThread]
    public static new void Main()
    {
        new LabApp().Run();
    }
}
