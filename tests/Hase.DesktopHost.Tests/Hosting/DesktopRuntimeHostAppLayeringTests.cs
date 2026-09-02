using System.IO;

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

    [Fact]
    public void Source_NamesNoInstrumentAnywhere()
    {
        // A reference guard cannot see a string. The KEL-103 operating
        // surface reached the published Runtime Host as hard-coded labels, a
        // hard-coded command path and a safety warning naming the device,
        // and every assembly-level guard passed while it did.
        string application = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "Hase.DesktopHost.App");
        string[] offenders =
            Directory
                .EnumerateFiles(application, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                .Where(path => !IsBuildOutput(path, application))
                .Where(path => NamesAnInstrument(File.ReadAllText(path)))
                .Select(path => Path.GetRelativePath(application, path))
                .Order()
                .ToArray();

        Assert.Empty(offenders);
    }

    private static readonly string[] InstrumentNames =
        ["Kel103", "KEL-103", "RfLab", "RF-Lab", "Mcnf"];

    private static bool NamesAnInstrument(string content) =>
        InstrumentNames.Any(name =>
            content.Contains(name, StringComparison.OrdinalIgnoreCase));

    private static bool IsBuildOutput(string path, string application)
    {
        string relative =
            Path.GetRelativePath(application, path);

        return relative.StartsWith(
                "obj" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
            || relative.StartsWith(
                "bin" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HASE.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
