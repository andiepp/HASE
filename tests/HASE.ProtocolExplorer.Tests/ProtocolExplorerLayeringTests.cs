using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

/// <summary>
/// Pins that the published protocol explorer names no instrument.
/// </summary>
/// <remarks>
/// The generic exploration surface stays public and the instrument
/// characterization leaves with the add-on. A reference reintroduced here
/// would put a private laboratory back into a published repository, so it
/// breaks a build rather than merely offending a principle.
/// </remarks>
public sealed class ProtocolExplorerLayeringTests
{
    [Fact]
    public void Assembly_DoesNotReferenceAnyInstrument()
    {
        string[] references = typeof(IScenario).Assembly
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

    [Fact]
    public void Scenarios_DeclareNoInstrumentName()
    {
        string[] scenarioTypeNames = typeof(IScenario).Assembly
            .GetTypes()
            .Where(type => typeof(IScenario).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToArray();

        Assert.NotEmpty(scenarioTypeNames);
        Assert.DoesNotContain(
            scenarioTypeNames,
            name => name.Contains("Kel103", StringComparison.OrdinalIgnoreCase));
    }
}
