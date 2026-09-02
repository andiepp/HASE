namespace Hase.DesktopHost.EndpointProfileTool.Tests;

/// <summary>
/// Pins that the published composition tool names no instrument.
/// </summary>
/// <remarks>
/// The published tool edits the endpoint kinds that carry no device
/// knowledge and migrates the composition format. A composition root that
/// ships instruments contributes their operations, so a reference
/// reintroduced here would put a private laboratory back into a published
/// repository.
/// </remarks>
public sealed class EndpointProfileToolLayeringTests
{
    [Fact]
    public void Assembly_DoesNotReferenceAnyInstrument()
    {
        string[] references =
            typeof(EndpointProfileToolApplication).Assembly
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
    public void ContributedOperationsAreWhatMakesAnInstrumentEditable()
    {
        // The contract exists so an entry point can add operations the
        // published tool does not have. If this type ever gains an
        // instrument-named implementation in this assembly, the split has
        // leaked back.
        string[] implementations =
            typeof(EndpointProfileToolApplication).Assembly
                .GetTypes()
                .Where(type =>
                    typeof(IEndpointProfileOperation).IsAssignableFrom(type)
                    && !type.IsInterface)
                .Select(type => type.Name)
                .ToArray();

        Assert.Empty(implementations);
    }
}
