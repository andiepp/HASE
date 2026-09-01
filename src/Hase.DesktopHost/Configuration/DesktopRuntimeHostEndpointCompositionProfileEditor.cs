using System.IO;
using System.Text.Json;
using Hase.Core.Domain.Descriptors;

namespace Hase.DesktopHost.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileEditor
{
    public Task MigrateKel103DefinitionAsync(
        string profilePath,
        string backupPath,
        string expectedEndpointId,
        DescriptorReference expectedCurrentDefinition,
        DescriptorReference replacementDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        ArgumentNullException.ThrowIfNull(expectedCurrentDefinition);
        ArgumentNullException.ThrowIfNull(replacementDefinition);
        if (expectedCurrentDefinition == replacementDefinition)
        {
            throw new ArgumentException(
                "Current and replacement definitions must be distinct.",
                nameof(replacementDefinition));
        }

        return EditAsync(profilePath, backupPath, profile =>
        {
            DesktopRuntimeHostKel103SerialEndpointProfile? existing =
                profile.Kel103SerialEndpoints.SingleOrDefault(endpoint =>
                    endpoint.ExpectedEndpointId == expectedEndpointId);
            if (existing is null)
            {
                throw new KeyNotFoundException(
                    "The KEL-103 serial endpoint profile is not registered.");
            }

            if (existing.DefinitionReference != expectedCurrentDefinition)
            {
                throw new InvalidOperationException(
                    "The KEL-103 serial endpoint does not use the required current definition.");
            }

            var migrated = new DesktopRuntimeHostKel103SerialEndpointProfile(
                existing.ExpectedEndpointId,
                replacementDefinition.Id.Value,
                replacementDefinition.Version,
                existing.SerialPort,
                existing.BaudRate);
            return Replace(
                profile,
                DesktopRuntimeHostEndpointCompositionProfile.Kel103SerialProviderId,
                expectedEndpointId,
                DesktopRuntimeHostEndpointCompositionProfile
                    .CreateKel103SerialEntry(migrated));
        }, cancellationToken);
    }

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

    public Task AddKel103Async(string profilePath, string backupPath,
        DesktopRuntimeHostKel103SerialEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile => Add(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile
                .CreateKel103SerialEntry(endpoint)), cancellationToken);
    }

    public Task RemoveKel103Async(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile => Remove(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile.Kel103SerialProviderId,
            expectedEndpointId,
            "The KEL-103 serial endpoint profile is not registered."), cancellationToken);
    }

    public Task AddRfLabAsync(string profilePath, string backupPath,
        DesktopRuntimeHostRfLabSerialEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile => Add(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile
                .CreateRfLabSerialEntry(endpoint)), cancellationToken);
    }

    public Task RemoveRfLabAsync(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile => Remove(
            profile,
            DesktopRuntimeHostEndpointCompositionProfile.RfLabSerialProviderId,
            expectedEndpointId,
            "The RF-Lab serial endpoint profile is not registered."), cancellationToken);
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

        return new DesktopRuntimeHostEndpointCompositionProfile(endpoints);
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
                !Matches(endpoint, providerId, expectedEndpointId)));
    }

    private static DesktopRuntimeHostEndpointCompositionProfile Replace(
        DesktopRuntimeHostEndpointCompositionProfile profile,
        string providerId,
        string expectedEndpointId,
        DesktopRuntimeHostEndpointEntry replacement) =>
        new(profile.Endpoints.Select(endpoint =>
            Matches(endpoint, providerId, expectedEndpointId)
                ? replacement
                : endpoint));

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
            new { formatVersion = 1, endpoints },
            new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

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
            _ => new
            {
                kind,
                expectedEndpointId = endpoint.ExpectedEndpointId,
                definitionId = endpoint.RequireString("definitionId"),
                definitionVersion = endpoint.RequireUInt16("definitionVersion"),
                serialPort = endpoint.RequireString("serialPort"),
                baudRate = endpoint.RequireInt32("baudRate")
            }
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
