namespace Hase.Client.Deployment;

public sealed record HaseClientGuidedInstallationPlan
{
    public HaseClientGuidedInstallationPlan(
        string installationDirectory,
        string configurationSourceFilePath,
        string desktopDirectory)
    {
        InstallationDirectory = NormalizeDirectory(
            installationDirectory,
            nameof(installationDirectory),
            "installation directory");
        ConfigurationSourceFilePath = NormalizeFile(
            configurationSourceFilePath,
            nameof(configurationSourceFilePath));
        DesktopDirectory = NormalizeDirectory(
            desktopDirectory,
            nameof(desktopDirectory),
            "desktop directory");
        ApplicationDirectory = Path.Combine(InstallationDirectory, "Application");
        ConfigurationDirectory = Path.Combine(InstallationDirectory, "Configuration");
        ExecutableFilePath = Path.Combine(ApplicationDirectory, "Hase.Client.Wpf.App.exe");
        ConfigurationFilePath = Path.Combine(ConfigurationDirectory, "laptop-private-network.json");
        ShortcutFilePath = Path.Combine(DesktopDirectory, "HASE Client.lnk");
        Shortcut = new HaseClientShortcutPlan(
            "HASE Client",
            ExecutableFilePath,
            $"\"{ConfigurationFilePath}\"",
            ApplicationDirectory);
    }

    public string InstallationDirectory { get; }
    public string ConfigurationSourceFilePath { get; }
    public string DesktopDirectory { get; }
    public string ApplicationDirectory { get; }
    public string ConfigurationDirectory { get; }
    public string ExecutableFilePath { get; }
    public string ConfigurationFilePath { get; }
    public string ShortcutFilePath { get; }
    public HaseClientShortcutPlan Shortcut { get; }

    public override string ToString() => "Guided HASE Client installation plan";

    private static string NormalizeDirectory(string path, string parameterName, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"The {role} must be fully qualified.", parameterName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static string NormalizeFile(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "The client configuration source path must be fully qualified.",
                parameterName);
        }

        return Path.GetFullPath(path);
    }
}

public sealed record HaseClientShortcutPlan(
    string Name,
    string TargetFilePath,
    string Arguments,
    string WorkingDirectory);
