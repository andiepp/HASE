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
            "-ApplicationProject $installedApplication.Project",
            script);
        Assert.DoesNotContain(
            "& $publisherPath -InstallationDirectory $installationDirectory\n",
            script.ReplaceLineEndings("\n"));
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
