namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103DependencyBoundaryTests
{
    [Fact]
    public void Assembly_DoesNotReferenceDeferredLayers()
    {
        string[] references = typeof(Kel103IdentityDefinition)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name == "Hase.Transport");
        Assert.DoesNotContain(references, name => name.StartsWith("Hase.Runtime", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name == "HASE.ProtocolExplorer");
    }
}
