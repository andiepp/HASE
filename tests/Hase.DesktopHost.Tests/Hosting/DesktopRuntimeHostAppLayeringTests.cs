using Hase.DesktopHost.App.Hosting;

namespace Hase.DesktopHost.Tests.Hosting;

/// <summary>
/// Pins that the published Runtime Host application names no instrument.
/// </summary>
/// <remarks>
/// The application composes the endpoint kinds that carry no device
/// knowledge; a composition root that ships instruments registers theirs
/// alongside. A reference reintroduced here would put a private laboratory
/// back into a published repository, so it breaks a build rather than
/// merely offending a principle.
/// </remarks>
public sealed class DesktopRuntimeHostAppLayeringTests
{
    [Fact]
    public void Assembly_DoesNotReferenceAnyInstrument()
    {
        string[] references =
            typeof(ProductionPrivateNetworkRuntimeHostBackend).Assembly
                .GetReferencedAssemblies()
                .Select(value => value.Name ?? string.Empty)
                .ToArray();

        Assert.DoesNotContain(
            references,
            name => name.Contains("Kel103", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("RfLab", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Mcnf", StringComparison.OrdinalIgnoreCase));
    }
}
