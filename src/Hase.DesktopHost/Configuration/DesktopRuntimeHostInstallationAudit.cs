using Hase.Runtime.Northbound;
using Hase.Runtime.Remote.Grpc.Adapter;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.DesktopHost.Configuration;

/// <summary>
/// Strictly audits one existing guided Runtime Host installation without
/// changing files, credentials, enrollment, or endpoint lifecycle state.
/// </summary>
public static class DesktopRuntimeHostInstallationAudit
{
    public static async Task<DesktopRuntimeHostInstallationAuditResult> AuditAsync(
        string installationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationDirectory);
        if (!Path.IsPathFullyQualified(installationDirectory))
        {
            throw new ArgumentException(
                "The Runtime Host installation directory must be fully qualified.",
                nameof(installationDirectory));
        }

        string installation = Path.GetFullPath(installationDirectory);
        string applicationDirectory = Path.Combine(installation, "Application");
        string configurationDirectory = Path.Combine(installation, "Configuration");
        string identityDirectory = Path.Combine(installation, "Identity");
        string executablePath = Path.Combine(applicationDirectory, "Hase.DesktopHost.App.exe");
        string profilePath = Path.Combine(configurationDirectory, "desktop-runtime-host.json");

        RequireFile(executablePath, "application executable");
        RequireFile(profilePath, "application profile");

        DesktopRuntimeHostInstallationProfile profile =
            await DesktopRuntimeHostInstallationProfileFile.LoadAsync(
                profilePath,
                cancellationToken).ConfigureAwait(false);

        string expectedIdentityPath = Path.Combine(identityDirectory, "runtime-host-identity.json");
        string expectedPrivateNetworkPath = Path.Combine(configurationDirectory, "desktop-private-network.json");
        string expectedEndpointCompositionPath = Path.Combine(configurationDirectory, "desktop-runtime-endpoints.json");

        RequireEqualPath(profile.IdentityFilePath, expectedIdentityPath, "identity file");
        RequireEqualPath(profile.PrivateNetworkConfigurationFilePath, expectedPrivateNetworkPath, "private-network configuration");
        RequireEqualPath(profile.EndpointCompositionFilePath, expectedEndpointCompositionPath, "endpoint-composition profile");

        RuntimeHostId runtimeHostId =
            await RuntimeHostIdentityReader.ReadAsync(expectedIdentityPath, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidDataException("The installed Runtime Host identity file is missing.");

        _ = await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                expectedEndpointCompositionPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (profile.MediaConfigurationFilePath is string mediaPath)
        {
            string expectedMediaPath = Path.Combine(
                configurationDirectory, "desktop-runtime-media.json");
            RequireEqualPath(mediaPath, expectedMediaPath,
                "media-configuration profile");
            RequireFile(expectedMediaPath, "media configuration");
            _ = await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                expectedMediaPath, cancellationToken).ConfigureAwait(false);
        }
        RuntimeHostPrivateNetworkDeploymentOptions privateNetwork =
            await RuntimeHostPrivateNetworkDeploymentOptionsFile.LoadAsync(
                expectedPrivateNetworkPath,
                cancellationToken).ConfigureAwait(false);
        RequireFile(privateNetwork.ClientEnrollmentFilePath, "client-enrollment configuration");
        _ = await RuntimeHostClientCredentialEnrollmentRegistryFile.LoadAsync(
                privateNetwork.ClientEnrollmentFilePath,
                cancellationToken)
            .ConfigureAwait(false);

        return new DesktopRuntimeHostInstallationAuditResult(runtimeHostId);
    }

    private static void RequireFile(string path, string role)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The installed Runtime Host {role} is missing.");
        }
    }

    private static void RequireEqualPath(string actual, string expected, string role)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), comparison))
        {
            throw new InvalidDataException(
                $"The installed Runtime Host {role} path is inconsistent with guided installation custody.");
        }
    }
}
