using Hase.Client.Wpf.AppHost;
using Xunit;

namespace Hase.Client.Wpf.Tests;

/// <summary>
/// Pins that the published Client application names no instrument.
/// </summary>
/// <remarks>
/// The Client library owns the panel registry and ships no panel; this
/// application ships none either. A composition root that ships panels
/// registers them. A reference reintroduced here would put a private
/// laboratory back into a published repository, so it breaks a build
/// rather than merely offending a principle.
/// </remarks>
public sealed class ClientApplicationLayeringTests
{
    [Fact]
    public void Assembly_DoesNotReferenceAnyInstrument()
    {
        string[] references = typeof(App).Assembly
            .GetReferencedAssemblies()
            .Select(value => value.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            name => name.Contains("RfLab", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Kel103", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Mcnf", StringComparison.OrdinalIgnoreCase));
    }
}
