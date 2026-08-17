namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostUpdatePlan
{
    public DesktopRuntimeHostUpdatePlan(
        string installationDirectory,
        string desktopDirectory)
    {
        InstallationDirectory = NormalizeDirectory(
            installationDirectory,
            nameof(installationDirectory),
            "installation directory");
        DesktopDirectory = NormalizeDirectory(
            desktopDirectory,
            nameof(desktopDirectory),
            "desktop directory");
        ApplicationDirectory = Path.Combine(InstallationDirectory, "Application");
        ConfigurationDirectory = Path.Combine(InstallationDirectory, "Configuration");
        IdentityDirectory = Path.Combine(InstallationDirectory, "Identity");
        ExecutableFilePath = Path.Combine(ApplicationDirectory, "Hase.DesktopHost.App.exe");
        ApplicationProfileFilePath = Path.Combine(ConfigurationDirectory, "desktop-runtime-host.json");
        EndpointCompositionFilePath = Path.Combine(ConfigurationDirectory, "desktop-runtime-endpoints.json");
        PrivateNetworkConfigurationFilePath = Path.Combine(ConfigurationDirectory, "desktop-private-network.json");
        MediaConfigurationFilePath = Path.Combine(ConfigurationDirectory, "desktop-runtime-media.json");
        IdentityFilePath = Path.Combine(IdentityDirectory, "runtime-host-identity.json");
        ShortcutFilePath = Path.Combine(DesktopDirectory, "HASE Runtime Host.lnk");
        ExpectedShortcut = new DesktopRuntimeHostShortcutPlan(
            "HASE Runtime Host",
            ExecutableFilePath,
            $"\"{ApplicationProfileFilePath}\"",
            ApplicationDirectory);
        PreservedFilePaths =
        [
            ApplicationProfileFilePath,
            EndpointCompositionFilePath,
            PrivateNetworkConfigurationFilePath,
            MediaConfigurationFilePath,
            ShortcutFilePath
        ];
    }

    public string InstallationDirectory { get; }
    public string DesktopDirectory { get; }
    public string ApplicationDirectory { get; }
    public string ConfigurationDirectory { get; }
    public string IdentityDirectory { get; }
    public string ExecutableFilePath { get; }
    public string ApplicationProfileFilePath { get; }
    public string EndpointCompositionFilePath { get; }
    public string PrivateNetworkConfigurationFilePath { get; }
    public string MediaConfigurationFilePath { get; }
    public string IdentityFilePath { get; }
    public string ShortcutFilePath { get; }
    public DesktopRuntimeHostShortcutPlan ExpectedShortcut { get; }
    public IReadOnlyList<string> PreservedFilePaths { get; }

    public override string ToString() => "Desktop Runtime Host application update plan";

    private static string NormalizeDirectory(string path, string parameterName, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"The {role} must be fully qualified.", parameterName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
