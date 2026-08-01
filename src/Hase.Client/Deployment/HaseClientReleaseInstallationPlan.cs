namespace Hase.Client.Deployment;

/// <summary>
/// Defines the filesystem boundary for one HASE Client Release installation
/// without performing publication or filesystem mutation.
/// </summary>
public sealed record HaseClientReleaseInstallationPlan
{
    public HaseClientReleaseInstallationPlan(
        string repositoryRoot,
        string installationDirectory)
    {
        RepositoryRoot = NormalizeDirectory(
            repositoryRoot,
            nameof(repositoryRoot),
            "repository root");
        InstallationDirectory = NormalizeDirectory(
            installationDirectory,
            nameof(installationDirectory),
            "installation directory");

        if (IsFilesystemRoot(InstallationDirectory))
        {
            throw new ArgumentException(
                "The installation directory must not be a filesystem root.",
                nameof(installationDirectory));
        }

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string repositoryPrefix = EnsureTrailingSeparator(RepositoryRoot);

        if (string.Equals(InstallationDirectory, RepositoryRoot, comparison)
            || EnsureTrailingSeparator(InstallationDirectory)
                .StartsWith(repositoryPrefix, comparison))
        {
            throw new ArgumentException(
                "The installation directory must not be the repository or a directory inside it.",
                nameof(installationDirectory));
        }

        ApplicationDirectory = Path.Combine(InstallationDirectory, "Application");
        ConfigurationDirectory = Path.Combine(InstallationDirectory, "Configuration");
        ExecutableFilePath = Path.Combine(ApplicationDirectory, "Hase.Client.Wpf.App.exe");
    }

    public string RepositoryRoot { get; }
    public string InstallationDirectory { get; }
    public string ApplicationDirectory { get; }
    public string ConfigurationDirectory { get; }
    public string ExecutableFilePath { get; }

    public override string ToString() => "HASE Client Release installation plan";

    private static string NormalizeDirectory(string path, string parameterName, string role)
    {
        ArgumentNullException.ThrowIfNull(path, parameterName);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"The {role} must not be empty or whitespace.", parameterName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"The {role} must be fully qualified.", parameterName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsFilesystemRoot(string path) =>
        string.Equals(
            path,
            Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
