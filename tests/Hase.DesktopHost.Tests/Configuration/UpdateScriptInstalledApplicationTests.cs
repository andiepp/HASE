using System.IO;

namespace Hase.DesktopHost.Tests.Configuration;

/// <summary>
/// Pins that updating an installation updates the application that
/// installation holds.
/// </summary>
/// <remarks>
/// An updater that assumes the shipped application would republish the base
/// over an add-on installation, verify the wrong executable, and point the
/// shortcut at a file that is no longer there. Publication records which
/// application it installed; the updaters read that record and fall back to
/// the shipped application, which is what every installation predating the
/// record holds.
/// </remarks>
public sealed class UpdateScriptInstalledApplicationTests
{
    public static TheoryData<string> Updaters =>
        new()
        {
            "Update-HaseDesktopRuntimeHost.ps1",
            "Update-HaseClient.ps1"
        };

    public static TheoryData<string> Publishers =>
        new()
        {
            "Publish-HaseDesktopRuntimeHost.ps1",
            "Publish-HaseClient.ps1"
        };

    public static TheoryData<string, string, string> UpdaterDefaults =>
        new()
        {
            {
                "Update-HaseDesktopRuntimeHost.ps1",
                "Hase.DesktopHost.App.exe",
                "src\\Hase.DesktopHost.App\\Hase.DesktopHost.App.csproj"
            },
            {
                "Update-HaseClient.ps1",
                "Hase.Client.Wpf.App.exe",
                "src\\Hase.Client.Wpf.App\\Hase.Client.Wpf.App.csproj"
            }
        };

    [Theory]
    [MemberData(nameof(Publishers))]
    public void Publisher_RecordsTheProjectAsWellAsTheExecutable(
        string scriptFileName)
    {
        // The executable alone cannot tell an update what to rebuild.
        string script = ReadScript(scriptFileName);

        Assert.Contains("applicationProject = $applicationProjectRelative", script);
        Assert.Contains("applicationProjectRoot = $applicationProjectRoot", script);
        Assert.Contains(
            "$applicationProjectRelative = $projectFile.Substring($applicationProjectRootPath.Length)",
            script);
    }

    [Theory]
    [MemberData(nameof(UpdaterDefaults))]
    public void Updater_ReadsTheRecordAndFallsBackToTheShippedApplication(
        string scriptFileName,
        string defaultExecutable,
        string defaultProject)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains("Get-InstalledApplication", script);
        Assert.Contains("installed-application.json", script);
        Assert.Contains($"-DefaultExecutableName \"{defaultExecutable}\"", script);
        Assert.Contains($"-DefaultProject \"{defaultProject}\"", script);
    }

    [Theory]
    [MemberData(nameof(Updaters))]
    public void Updater_RepublishesTheApplicationItIsUpdating(
        string scriptFileName)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains(
            "-ApplicationProject $applicationProjectToPublish",
            script);
        Assert.Contains(
            "$installedApplication.Project",
            script);
        Assert.DoesNotContain(
            "& $publisherPath -InstallationDirectory $installationDirectory\n",
            script.ReplaceLineEndings("\n"));
    }

    [Theory]
    [MemberData(nameof(Updaters))]
    public void Updater_CanBeToldWhatTheInstallationShouldHold(
        string scriptFileName)
    {
        // An installation that predates the record, or that is changing to
        // another application, is told once; the publisher records it and
        // every later update reads the record.
        string script = ReadScript(scriptFileName);

        Assert.Contains("[string]$ApplicationProject", script);
        Assert.Contains(
            "$applicationProjectToPublish = if ([string]::IsNullOrWhiteSpace($ApplicationProject))",
            script);
    }

    [Theory]
    [MemberData(nameof(Updaters))]
    public void Updater_VerifiesWhatWasRecordedNotWhatWasAssumed(
        string scriptFileName)
    {
        // The checks before publication run against the installed
        // application; the check after runs against what the publisher
        // recorded, because the two differ the first time an installation
        // changes to an application of another name.
        string script = ReadScript(scriptFileName);

        int published = script.IndexOf(
            "-ApplicationProject $applicationProjectToPublish",
            StringComparison.Ordinal);
        int reread = script.IndexOf(
            "$updatedApplication = Get-InstalledApplication",
            StringComparison.Ordinal);
        int verified = script.IndexOf(
            "Test-Path -LiteralPath $updatedExecutableFilePath -PathType Leaf",
            StringComparison.Ordinal);

        Assert.True(published > 0);
        Assert.True(reread > published);
        Assert.True(verified > reread);
    }

    [Theory]
    [MemberData(nameof(Updaters))]
    public void Updater_RepointsTheShortcutOnlyWhenTheExecutableChangesName(
        string scriptFileName)
    {
        // Custody is preserved; the one exception is a shortcut whose target
        // no longer exists under that name, which is re-pointed and verified
        // to have changed in nothing else.
        string script = ReadScript(scriptFileName);

        Assert.Contains("$shortcutRepointed = $false", script);
        Assert.Contains("$shortcut.TargetPath = $updatedExecutableFilePath", script);
        Assert.Contains("$shortcut.IconLocation = $updatedExecutableFilePath", script);
        Assert.Contains("-Role \"re-pointed shortcut target\"", script);
        Assert.Contains("-Role \"re-pointed shortcut working directory\"", script);
        Assert.Contains("(-not $shortcutRepointed -and", script);
        Assert.DoesNotContain("$shortcut.Arguments = ", script);
        Assert.DoesNotContain("$shortcut.WorkingDirectory = ", script);
    }

    [Theory]
    [MemberData(nameof(Updaters))]
    public void Updater_DerivesEveryInstalledPathFromTheRecord(
        string scriptFileName)
    {
        string script = ReadScript(scriptFileName);

        Assert.Contains("$executableName = $installedApplication.ExecutableName", script);
        Assert.Contains(
            "$executableFilePath = Join-Path $applicationDirectory $executableName",
            script);
    }

    [Fact]
    public void Updater_RejectsARecordThatNamesNoExecutable()
    {
        foreach (string scriptFileName in
            new[] { "Update-HaseDesktopRuntimeHost.ps1", "Update-HaseClient.ps1" })
        {
            Assert.Contains(
                "The installed-application record names no executable.",
                ReadScript(scriptFileName));
        }
    }

    [Fact]
    public void ClientUpdater_GuardsAgainstWhicheverClientIsInstalled()
    {
        // A fixed process name stops protecting anything the moment the
        // installation holds an add-on client, which is the defect the
        // composition tool carried until it was found.
        string script = ReadScript("Update-HaseClient.ps1");

        Assert.Contains(
            "-Name ([System.IO.Path]::GetFileNameWithoutExtension($executableName))",
            script);
        Assert.DoesNotContain("-Name \"Hase.Client.Wpf.App\"", script);
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
