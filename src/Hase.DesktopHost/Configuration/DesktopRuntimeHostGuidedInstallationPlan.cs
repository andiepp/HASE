using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Configuration;

public sealed record DesktopRuntimeHostGuidedInstallationPlan
{
    public DesktopRuntimeHostGuidedInstallationPlan(
        string installationDirectory,
        string privateNetworkConfigurationSourceFilePath,
        string nativeEndpointHost)
        : this(
            installationDirectory,
            privateNetworkConfigurationSourceFilePath,
            nativeEndpointHost,
            new DesktopRuntimeHostEndpointCompositionProfile(
                [new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "doit-esp32-devkitc-v4-01", nativeEndpointHost, 5000)],
                [new DesktopRuntimeHostCompactSerialEndpointProfile(
                    "arduino-uno-01", 0x2341, 0x0043, 115200,
                    TimeSpan.FromSeconds(3))]))
    {
    }

    public DesktopRuntimeHostGuidedInstallationPlan(
        string installationDirectory,
        string privateNetworkConfigurationSourceFilePath,
        string compactExpectedEndpointId,
        ushort compactVendorId,
        ushort compactProductId,
        int compactBaudRate,
        TimeSpan compactVerificationTimeout)
        : this(
            installationDirectory,
            privateNetworkConfigurationSourceFilePath,
            null,
            new DesktopRuntimeHostEndpointCompositionProfile(
                [],
                [new DesktopRuntimeHostCompactSerialEndpointProfile(
                    compactExpectedEndpointId,
                    compactVendorId,
                    compactProductId,
                    compactBaudRate,
                    compactVerificationTimeout)]))
    {
    }

    private DesktopRuntimeHostGuidedInstallationPlan(
        string installationDirectory,
        string privateNetworkConfigurationSourceFilePath,
        string? nativeEndpointHost,
        DesktopRuntimeHostEndpointCompositionProfile endpointComposition)
    {
        InstallationDirectory = NormalizeDirectory(
            installationDirectory,
            nameof(installationDirectory));
        PrivateNetworkConfigurationSourceFilePath = NormalizeFile(
            privateNetworkConfigurationSourceFilePath,
            nameof(privateNetworkConfigurationSourceFilePath));
        NativeEndpointHost = nativeEndpointHost?.Trim();
        ApplicationDirectory = Path.Combine(InstallationDirectory, "Application");
        ConfigurationDirectory = Path.Combine(InstallationDirectory, "Configuration");
        IdentityDirectory = Path.Combine(InstallationDirectory, "Identity");
        ExecutableFilePath = Path.Combine(ApplicationDirectory, "Hase.DesktopHost.App.exe");
        ApplicationProfileFilePath = Path.Combine(ConfigurationDirectory, "desktop-runtime-host.json");
        EndpointCompositionFilePath = Path.Combine(ConfigurationDirectory, "desktop-runtime-endpoints.json");
        PrivateNetworkConfigurationFilePath = Path.Combine(ConfigurationDirectory, "desktop-private-network.json");
        IdentityFilePath = Path.Combine(IdentityDirectory, "runtime-host-identity.json");

        InstallationProfile = new DesktopRuntimeHostInstallationProfile(
            IdentityFilePath,
            PrivateNetworkConfigurationFilePath,
            EndpointCompositionFilePath,
            RuntimeDiagnosticLevel.Bytes,
            includeByteBufferSimulation: false);
        EndpointComposition = endpointComposition;
        Shortcut = new DesktopRuntimeHostShortcutPlan(
            "HASE Runtime Host",
            ExecutableFilePath,
            $"\"{ApplicationProfileFilePath}\"",
            ApplicationDirectory);
    }

    public string InstallationDirectory { get; }
    public string PrivateNetworkConfigurationSourceFilePath { get; }
    public string? NativeEndpointHost { get; }
    public string ApplicationDirectory { get; }
    public string ConfigurationDirectory { get; }
    public string IdentityDirectory { get; }
    public string ExecutableFilePath { get; }
    public string ApplicationProfileFilePath { get; }
    public string EndpointCompositionFilePath { get; }
    public string PrivateNetworkConfigurationFilePath { get; }
    public string IdentityFilePath { get; }
    public DesktopRuntimeHostInstallationProfile InstallationProfile { get; }
    public DesktopRuntimeHostEndpointCompositionProfile EndpointComposition { get; }
    public DesktopRuntimeHostShortcutPlan Shortcut { get; }

    public override string ToString() => "Guided Desktop Runtime Host installation plan";

    private static string NormalizeDirectory(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The installation directory must be fully qualified.", parameterName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static string NormalizeFile(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The private-network configuration source path must be fully qualified.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}

public sealed record DesktopRuntimeHostShortcutPlan(
    string Name,
    string TargetFilePath,
    string Arguments,
    string WorkingDirectory);
