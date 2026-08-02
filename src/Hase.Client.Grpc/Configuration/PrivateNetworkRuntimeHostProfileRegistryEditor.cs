using System.Text.Json;
using Hase.Client.Configuration;
using Hase.Runtime.Northbound;

namespace Hase.Client.Grpc.Configuration;

/// <summary>
/// Applies explicit offline profile changes through strict validation and an
/// atomic registry replacement that retains the previous file as a backup.
/// </summary>
public sealed class PrivateNetworkRuntimeHostProfileRegistryEditor
{
    public async Task<RuntimeHostId> AddFromHandoffAsync(
        string registryFilePath,
        string backupFilePath,
        string handoffFilePath,
        RuntimeHostProfileId profileId,
        string displayName,
        string privateNetworkConfigurationFilePath,
        CancellationToken cancellationToken = default)
        => await AddFromHandoffCoreAsync(
            registryFilePath,
            backupFilePath,
            handoffFilePath,
            profileId,
            displayName,
            privateNetworkConfigurationFilePath,
            isEnabled: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async Task<RuntimeHostId> AddEnabledFromHandoffAsync(
        string registryFilePath,
        string backupFilePath,
        string handoffFilePath,
        RuntimeHostProfileId profileId,
        string displayName,
        string privateNetworkConfigurationFilePath,
        CancellationToken cancellationToken = default)
        => await AddFromHandoffCoreAsync(
            registryFilePath,
            backupFilePath,
            handoffFilePath,
            profileId,
            displayName,
            privateNetworkConfigurationFilePath,
            isEnabled: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    private async Task<RuntimeHostId> AddFromHandoffCoreAsync(
        string registryFilePath,
        string backupFilePath,
        string handoffFilePath,
        RuntimeHostProfileId profileId,
        string displayName,
        string privateNetworkConfigurationFilePath,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        RuntimeHostOnboardingHandoff handoff =
            await RuntimeHostOnboardingHandoffFile.LoadAsync(
                handoffFilePath,
                cancellationToken).ConfigureAwait(false);

        await AddAsync(
            registryFilePath,
            backupFilePath,
            new PrivateNetworkRuntimeHostProfile(
                new RuntimeHostProfile(
                    profileId,
                    displayName,
                    new RemoteRuntimeHostId(handoff.RuntimeHostId.Value),
                    isEnabled),
                privateNetworkConfigurationFilePath),
            cancellationToken).ConfigureAwait(false);

        return handoff.RuntimeHostId;
    }

    public async Task AddAsync(
        string registryFilePath,
        string backupFilePath,
        PrivateNetworkRuntimeHostProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!File.Exists(profile.PrivateNetworkConfigurationFilePath))
        {
            throw new FileNotFoundException(
                "The referenced private-network client configuration does not exist.",
                profile.PrivateNetworkConfigurationFilePath);
        }

        await EditAsync(
            registryFilePath,
            backupFilePath,
            registry => new PrivateNetworkRuntimeHostProfileRegistry(
                registry.Profiles.Concat([profile])),
            cancellationToken).ConfigureAwait(false);
    }

    public Task SetEnabledAsync(
        string registryFilePath,
        string backupFilePath,
        RuntimeHostProfileId profileId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        return EditAsync(
            registryFilePath,
            backupFilePath,
            registry => new PrivateNetworkRuntimeHostProfileRegistry(
                registry.Profiles.Select(profile =>
                    profile.Profile.ProfileId == profileId
                        ? WithEnabled(profile, enabled)
                        : profile)),
            cancellationToken,
            profileId);
    }

    public Task RemoveAsync(
        string registryFilePath,
        string backupFilePath,
        RuntimeHostProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        return EditAsync(
            registryFilePath,
            backupFilePath,
            registry => new PrivateNetworkRuntimeHostProfileRegistry(
                registry.Profiles.Where(
                    profile => profile.Profile.ProfileId != profileId)),
            cancellationToken,
            profileId);
    }

    private static async Task EditAsync(
        string registryFilePath,
        string backupFilePath,
        Func<PrivateNetworkRuntimeHostProfileRegistry, PrivateNetworkRuntimeHostProfileRegistry> edit,
        CancellationToken cancellationToken,
        RuntimeHostProfileId? requiredProfileId = null)
    {
        (string registryPath, string backupPath) = ValidatePaths(
            registryFilePath,
            backupFilePath);
        ArgumentNullException.ThrowIfNull(edit);
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(backupPath))
        {
            throw new IOException("The requested registry backup file already exists.");
        }

        PrivateNetworkRuntimeHostProfileRegistry current =
            await PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                registryPath,
                cancellationToken).ConfigureAwait(false);

        if (requiredProfileId is not null && !current.TryGet(requiredProfileId, out _))
        {
            throw new KeyNotFoundException(
                $"Runtime-host profile '{requiredProfileId}' is not registered.");
        }

        PrivateNetworkRuntimeHostProfileRegistry candidate = edit(current);
        string temporaryPath = Path.Combine(
            Path.GetDirectoryName(registryPath)!,
            $".{Path.GetFileName(registryPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await WriteNewAsync(temporaryPath, candidate, cancellationToken)
                .ConfigureAwait(false);
            _ = await PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                    temporaryPath,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(temporaryPath, registryPath, backupPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task WriteNewAsync(
        string filePath,
        PrivateNetworkRuntimeHostProfileRegistry registry,
        CancellationToken cancellationToken)
    {
        var document = new
        {
            formatVersion = 1,
            hosts = registry.Profiles.Select(profile => new
            {
                profileId = profile.Profile.ProfileId.Value,
                displayName = profile.Profile.DisplayName,
                expectedRuntimeHostId = profile.Profile.ExpectedRuntimeHostId.Value,
                privateNetworkConfigurationFilePath =
                    profile.PrivateNetworkConfigurationFilePath,
                enabled = profile.Profile.IsEnabled
            })
        };
        await using FileStream stream = new(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(
            stream,
            document,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static PrivateNetworkRuntimeHostProfile WithEnabled(
        PrivateNetworkRuntimeHostProfile profile,
        bool enabled) =>
        new(
            new RuntimeHostProfile(
                profile.Profile.ProfileId,
                profile.Profile.DisplayName,
                profile.Profile.ExpectedRuntimeHostId,
                enabled),
            profile.PrivateNetworkConfigurationFilePath);

    private static (string RegistryPath, string BackupPath) ValidatePaths(
        string registryFilePath,
        string backupFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        if (!Path.IsPathFullyQualified(registryFilePath)
            || !Path.IsPathFullyQualified(backupFilePath))
        {
            throw new ArgumentException("Registry and backup paths must be fully qualified.");
        }

        string registryPath = Path.GetFullPath(registryFilePath);
        string backupPath = Path.GetFullPath(backupFilePath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(registryPath, backupPath, comparison))
        {
            throw new ArgumentException("Registry and backup paths must be distinct.");
        }

        return (registryPath, backupPath);
    }
}
