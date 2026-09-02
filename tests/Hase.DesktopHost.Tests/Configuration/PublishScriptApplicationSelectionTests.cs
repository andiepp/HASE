using System.IO;

namespace Hase.DesktopHost.Tests.Configuration;

/// <summary>
/// Pins that publication names the application it publishes rather than
/// assuming the one this repository ships.
/// </summary>
/// <remarks>
/// The published tooling publishes the applications this repository ships. A
/// composition root that ships instruments publishes its own application, and
/// before this it could not: the project, the staged executable and the
/// installed executable were the base name written three times. The
/// installation records which application it holds so that the update path
/// need not assume either.
/// </remarks>
public sealed class PublishScriptApplicationSelectionTests
{
    public static TheoryData<string> Publishers =>
        new()
        {
            "Publish-HaseDesktopRuntimeHost.ps1",
            "Publish-HaseClient.ps1"
        };

    public static TheoryData<string, string> PublisherDefaults =>
        new()
        {
            {
                "Publish-HaseDesktopRuntimeHost.ps1",
                "src\\Hase.DesktopHost.App\\Hase.DesktopHost.App.csproj"
            },
            {
                "Publish-HaseClient.ps1",
                "src\\Hase.Client.Wpf.App\\Hase.Client.Wpf.App.csproj"
            }
        };

    public static TheoryData<string, string> PublisherRoles =>
        new()
        {
            { "Publish-HaseDesktopRuntimeHost.ps1", "Desktop Runtime Host" },
            { "Publish-HaseClient.ps1", "HASE Client" }
        };

    [Theory]
    [MemberData(nameof(PublisherDefaults))]
    public void Publisher_TakesTheApplicationProjectAndDefaultsToTheShippedOne(
        string scriptFileName,
        string defaultProjectFragment)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains("[string]$ApplicationProject", script);
        Assert.Contains("$defaultProjectFile = Join-Path $repositoryRoot", script);
        Assert.Contains(defaultProjectFragment, script);
        Assert.Contains("-RequestedProject $ApplicationProject", script);
    }

    [Theory]
    [MemberData(nameof(Publishers))]
    public void Publisher_RefusesAProjectOutsideTheRepositoryOrOfTheWrongKind(
        string scriptFileName)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains(
            "The application project must be inside this repository.",
            script);
        Assert.Contains(
            "The application project must be a .csproj file.",
            script);
    }

    [Theory]
    [MemberData(nameof(Publishers))]
    public void Publisher_DerivesEveryExecutableNameFromThatProject(
        string scriptFileName)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains(
            "$applicationName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile)",
            script);
        Assert.Contains("$executableName = \"$applicationName.exe\"", script);
        Assert.Contains(
            "$executableFile = Join-Path $applicationDirectory $executableName",
            script);
        Assert.Contains(
            "$stagedExecutable = Join-Path $stagingDirectory $executableName",
            script);
    }

    [Theory]
    [MemberData(nameof(Publishers))]
    public void Publisher_NamesNoExecutableLiterally(string scriptFileName)
    {
        // A literal executable name is the defect this closes: publication
        // would install an add-on application and then look for the base one.
        string script = ReadScript(scriptFileName);

        Assert.DoesNotContain("\"Hase.DesktopHost.App.exe\"", script);
        Assert.DoesNotContain("\"Hase.Client.Wpf.App.exe\"", script);
    }

    [Theory]
    [MemberData(nameof(PublisherRoles))]
    public void Publisher_RecordsTheInstalledApplicationOnlyAfterVerifyingIt(
        string scriptFileName,
        string applicationRole)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains("installed-application.json", script);
        Assert.Contains("applicationExecutable = $executableName", script);

        int verified = script.IndexOf(
            $"The installed {applicationRole} executable could not be verified.",
            StringComparison.Ordinal);
        int recorded = script.IndexOf(
            "applicationExecutable = $executableName",
            StringComparison.Ordinal);
        int backupRemoved = script.IndexOf(
            "Remove-Item -LiteralPath $backupDirectory -Recurse -Force",
            StringComparison.Ordinal);

        Assert.True(verified > 0);
        Assert.True(recorded > 0);
        Assert.True(backupRemoved > 0);

        // Recorded after the installed executable is verified, and before the
        // previous application is discarded, so a failure restores the
        // application and leaves the record still describing it.
        Assert.True(recorded > verified);
        Assert.True(recorded < backupRemoved);
    }

    private static string ReadScript(string name) =>
        File.ReadAllText(
            Path.Combine(GetRepositoryRoot(), "tools", "Deployment", name));

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
