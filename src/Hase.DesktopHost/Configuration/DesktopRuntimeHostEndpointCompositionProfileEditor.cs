using System.IO;
using System.Text.Json;

namespace Hase.DesktopHost.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileEditor
{
    /// <summary>
    /// Rewrites one composition in the provider-keyed shape, retaining the
    /// previous file as the supplied backup.
    /// </summary>
    /// <remarks>
    /// This is the only operation that changes a composition's format
    /// version. It refuses a composition already in the open shape rather
    /// than rewriting it, so re-running the migration on a migrated host is
    /// a no-op that reports why.
    /// </remarks>
    public Task MigrateToOpenFormatAsync(
        string profilePath,
        string backupPath,
        CancellationToken cancellationToken = default) =>
        EditAsync(profilePath, backupPath, profile =>
        {
            if (profile.FormatVersion
                == DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion)
            {
                throw new InvalidOperationException(
                    "The endpoint composition is already in the "
                    + "provider-keyed format.");
            }

            return profile with
            {
                FormatVersion =
                    DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion
            };
        }, cancellationToken);

    public Task AddCompactAsync(string profilePath, string backupPath,
        DesktopRuntimeHostCompactSerialEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile => Add(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile
                .CreateCompactSerialEntry(endpoint)), cancellationToken);
    }

    public Task RemoveCompactAsync(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile => Remove(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile.CompactSerialProviderId,
            expectedEndpointId,
            "The compact-serial endpoint profile is not registered."), cancellationToken);
    }

    public Task AddNativeAsync(string profilePath, string backupPath,
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile => Add(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile
                .CreateNativeNetworkEntry(endpoint)), cancellationToken);
    }

    public Task RemoveNativeAsync(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile => Remove(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile.NativeNetworkProviderId,
            expectedEndpointId,
            "The native-network endpoint profile is not registered."), cancellationToken);
    }

    /// <summary>
    /// Adds one endpoint after the last endpoint of the same provider, so an
    /// existing composition keeps its grouping.
    /// </summary>
    /// <remarks>
    /// Every edit rebuilds the composition from its entries rather than from
    /// the typed views, so an endpoint supplied by a provider this library
    /// does not know survives the edit instead of being quietly dropped.
    /// </remarks>
    /// <summary>
    /// Adds an endpoint of any provider. The entry is inserted after the
    /// last entry of its provider, or at the end when the provider has none;
    /// the composition's own rules reject a duplicate endpoint identity
    /// before anything is written.
    /// </summary>
    /// <remarks>
    /// The seam an add-on's tooling edits the composition through. The
    /// editor knows nothing about the provider or its settings; only that
    /// provider does.
    /// </remarks>
    public Task AddEntryAsync(string profilePath, string backupPath,
        DesktopRuntimeHostEndpointEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return EditAsync(profilePath, backupPath, profile => Add(
            profile,
            entry), cancellationToken);
    }

    /// <summary>
    /// Removes the endpoint of the given provider and identity. Absence is
    /// reported and nothing is written.
    /// </summary>
    public Task RemoveEntryAsync(string profilePath, string backupPath,
        string providerId, string expectedEndpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile => Remove(
            profile,
            providerId,
            expectedEndpointId,
            AbsentMessage(providerId, expectedEndpointId)), cancellationToken);
    }

    /// <summary>
    /// Replaces the endpoint of the given provider and identity with an entry
    /// of the same provider and identity, in place. Absence is reported and
    /// nothing is written; a replacement that would change the identity is
    /// rejected, because that is a removal and an addition, not an edit.
    /// </summary>
    public Task ReplaceEntryAsync(string profilePath, string backupPath,
        string providerId, string expectedEndpointId,
        DesktopRuntimeHostEndpointEntry replacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        ArgumentNullException.ThrowIfNull(replacement);
        if (!StringComparer.Ordinal.Equals(replacement.ProviderId, providerId)
            || !StringComparer.Ordinal.Equals(
                replacement.ExpectedEndpointId,
                expectedEndpointId))
        {
            throw new ArgumentException(
                "The replacement must keep the endpoint's provider and identity.",
                nameof(replacement));
        }

        return EditAsync(profilePath, backupPath, profile =>
        {
            if (!profile.Endpoints.Any(endpoint =>
                    Matches(endpoint, providerId, expectedEndpointId)))
            {
                throw new KeyNotFoundException(
                    AbsentMessage(providerId, expectedEndpointId));
            }

            return Replace(profile, providerId, expectedEndpointId, replacement);
        }, cancellationToken);
    }

    private static string AbsentMessage(string providerId, string expectedEndpointId) =>
        $"Endpoint '{expectedEndpointId}' of provider '{providerId}' is not registered.";

    private static DesktopRuntimeHostEndpointCompositionProfile Add(
        DesktopRuntimeHostEndpointCompositionProfile profile,
        DesktopRuntimeHostEndpointEntry addition)
    {
        var endpoints = profile.Endpoints.ToList();
        int lastOfProvider = endpoints.FindLastIndex(endpoint =>
            StringComparer.Ordinal.Equals(endpoint.ProviderId, addition.ProviderId));

        endpoints.Insert(
            lastOfProvider < 0 ? endpoints.Count : lastOfProvider + 1,
            addition);

        return new DesktopRuntimeHostEndpointCompositionProfile(endpoints)
        {
            FormatVersion = profile.FormatVersion
        };
    }

    private static DesktopRuntimeHostEndpointCompositionProfile Remove(
        DesktopRuntimeHostEndpointCompositionProfile profile,
        string providerId,
        string expectedEndpointId,
        string absentMessage)
    {
        if (!profile.Endpoints.Any(endpoint =>
                Matches(endpoint, providerId, expectedEndpointId)))
        {
            throw new KeyNotFoundException(absentMessage);
        }

        return new DesktopRuntimeHostEndpointCompositionProfile(
            profile.Endpoints.Where(endpoint =>
                !Matches(endpoint, providerId, expectedEndpointId)))
        {
            FormatVersion = profile.FormatVersion
        };
    }

    private static DesktopRuntimeHostEndpointCompositionProfile Replace(
        DesktopRuntimeHostEndpointCompositionProfile profile,
        string providerId,
        string expectedEndpointId,
        DesktopRuntimeHostEndpointEntry replacement) =>
        new(profile.Endpoints.Select(endpoint =>
            Matches(endpoint, providerId, expectedEndpointId)
                ? replacement
                : endpoint))
        {
            FormatVersion = profile.FormatVersion
        };

    private static bool Matches(
        DesktopRuntimeHostEndpointEntry endpoint,
        string providerId,
        string expectedEndpointId) =>
        StringComparer.Ordinal.Equals(endpoint.ProviderId, providerId)
        && StringComparer.Ordinal.Equals(
            endpoint.ExpectedEndpointId,
            expectedEndpointId);

    private static async Task EditAsync(string profileFilePath, string backupFilePath,
        Func<DesktopRuntimeHostEndpointCompositionProfile,
            DesktopRuntimeHostEndpointCompositionProfile> edit,
        CancellationToken cancellationToken)
    {
        string profilePath = Normalize(profileFilePath);
        string backupPath = Normalize(backupFilePath);
        if (PathComparer.Equals(profilePath, backupPath))
            throw new ArgumentException("Profile and backup paths must be distinct.");
        if (File.Exists(backupPath))
            throw new IOException("The requested endpoint-composition backup already exists.");

        DesktopRuntimeHostEndpointCompositionProfile current =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                profilePath, cancellationToken).ConfigureAwait(false);
        DesktopRuntimeHostEndpointCompositionProfile candidate = edit(current);
        string temporaryPath = Path.Combine(Path.GetDirectoryName(profilePath)!,
            $".{Path.GetFileName(profilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await WriteAsync(temporaryPath, candidate, cancellationToken).ConfigureAwait(false);
            _ = await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                temporaryPath, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(temporaryPath, profilePath, backupPath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Writes the composition in the version 1 shape, which is still what
    /// every installed host reads.
    /// </summary>
    /// <remarks>
    /// The endpoints are written from the open collection rather than from
    /// the typed views, so nothing an edit preserved can be dropped on the
    /// way out. A provider the closed format cannot name is refused rather
    /// than silently omitted; editing such a composition waits for the
    /// migration that lets the open shape be written.
    /// </remarks>
    private static async Task WriteAsync(string path,
        DesktopRuntimeHostEndpointCompositionProfile profile,
        CancellationToken cancellationToken)
    {
        var endpoints = new List<object>();

        foreach (DesktopRuntimeHostEndpointEntry endpoint in profile.Endpoints)
        {
            if (profile.FormatVersion
                == DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion)
            {
                endpoints.Add(CreateProviderEndpoint(endpoint));
                continue;
            }

            if (!DesktopRuntimeHostLegacyEndpointKinds.TryGetKind(
                    endpoint.ProviderId,
                    out string kind))
            {
                throw new InvalidDataException(
                    $"Endpoint '{endpoint.ExpectedEndpointId}' is supplied by "
                    + $"provider '{endpoint.ProviderId}', which the version 1 "
                    + "composition format cannot express.");
            }

            endpoints.Add(CreateLegacyEndpoint(kind, endpoint));
        }

        await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream,
            new { formatVersion = profile.FormatVersion, endpoints },
            new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one endpoint in the provider-keyed shape. Settings are written
    /// as the text the model carries, which the reader accepts alongside JSON
    /// numbers and booleans, so a migrated composition round-trips exactly.
    /// </summary>
    private static object CreateProviderEndpoint(
        DesktopRuntimeHostEndpointEntry endpoint) =>
        new
        {
            providerId = endpoint.ProviderId,
            expectedEndpointId = endpoint.ExpectedEndpointId,
            settings = endpoint.Settings.ToDictionary(
                setting => setting.Key,
                setting => setting.Value,
                StringComparer.Ordinal)
        };

    private static object CreateLegacyEndpoint(
        string kind,
        DesktopRuntimeHostEndpointEntry endpoint) =>
        kind switch
        {
            "NativeNetwork" => new
            {
                kind,
                expectedEndpointId = endpoint.ExpectedEndpointId,
                host = endpoint.RequireString("host"),
                port = endpoint.RequireInt32("port")
            },
            "CompactSerial" => new
            {
                kind,
                expectedEndpointId = endpoint.ExpectedEndpointId,
                vendorId = endpoint.RequireUInt16("vendorId"),
                productId = endpoint.RequireUInt16("productId"),
                baudRate = endpoint.RequireInt32("baudRate"),
                verificationTimeoutMilliseconds =
                    endpoint.RequireInt32("verificationTimeoutMilliseconds")
            },
            _ => throw new InvalidDataException(
                $"Endpoint kind '{kind}' cannot be written in the version 1 "
                + "composition format.")
        };

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Endpoint-composition paths must be fully qualified.");
        return Path.GetFullPath(path);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
