using System.IO;
using System.Text.RegularExpressions;

namespace Hase.DesktopHost.Tests;

/// <summary>
/// Pins that the solution names no project of the private laboratory.
/// </summary>
/// <remarks>
/// While the laboratory lived in this repository the base was defined as a
/// subtraction from the full solution and a second solution file carried
/// it. The laboratory now lives in its own repository, which consumes this
/// one as a submodule, so there is one solution and it must be the base: a
/// project of the laboratory reappearing here would put a private bench
/// back into a published repository, and this breaks a build rather than
/// merely offending a principle.
///
/// An instrument is an add-on; the protocol it speaks is not. <c>Hase.Scpi</c>
/// and <c>Hase.Mcnf</c> are published, exactly as <c>Hase.Scpi.Kel103</c> and
/// <c>Hase.Mcnf.RfLab</c> are not.
/// </remarks>
public sealed class BaseSolutionCompositionTests
{
    private static readonly Regex AddOnProject =
        new(@"Kel103|RfLab|\.Lab(\.|/)", RegexOptions.Compiled);

    [Fact]
    public void TheSolutionNamesNoProjectOfTheLaboratory()
    {
        Assert.DoesNotContain(
            ReadProjects("HASE.slnx"),
            path => AddOnProject.IsMatch(path));
    }

    [Fact]
    public void TheSolutionIsNotEmpty()
    {
        // The guard above is only meaningful over a real project list.
        Assert.NotEmpty(ReadProjects("HASE.slnx"));
    }

    private static IReadOnlyList<string> ReadProjects(string solutionFileName)
    {
        string solution = File.ReadAllText(
            Path.Combine(GetRepositoryRoot(), solutionFileName));

        return Regex.Matches(solution, @"<Project\s+Path=""([^""]+)""")
            .Select(match =>
                match.Groups[1].Value.Replace(Path.DirectorySeparatorChar, '/'))
            .ToArray();
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
