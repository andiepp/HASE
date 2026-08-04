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
            return new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints,
                profile.CompactSerialEndpoints,
                profile.Kel103SerialEndpoints.Select(endpoint =>
                    endpoint.ExpectedEndpointId == expectedEndpointId
                        ? migrated
                        : endpoint));
        }, cancellationToken);
    }

    public Task AddCompactAsync(string profilePath, string backupPath,
        DesktopRuntimeHostCompactSerialEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile =>
            new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints,
                profile.CompactSerialEndpoints.Concat([endpoint]),
                profile.Kel103SerialEndpoints), cancellationToken);
    }

    public Task RemoveCompactAsync(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile =>
        {
            if (!profile.CompactSerialEndpoints.Any(endpoint =>
                    endpoint.ExpectedEndpointId == expectedEndpointId))
                throw new KeyNotFoundException("The compact-serial endpoint profile is not registered.");
            return new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints,
                profile.CompactSerialEndpoints.Where(endpoint =>
                    endpoint.ExpectedEndpointId != expectedEndpointId),
                profile.Kel103SerialEndpoints);
        }, cancellationToken);
    }

    public Task AddNativeAsync(string profilePath, string backupPath,
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile =>
            new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints.Concat([endpoint]),
                profile.CompactSerialEndpoints,
                profile.Kel103SerialEndpoints), cancellationToken);
    }

    public Task RemoveNativeAsync(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile =>
        {
            if (!profile.NativeNetworkEndpoints.Any(endpoint =>
                    endpoint.ExpectedEndpointId == expectedEndpointId))
                throw new KeyNotFoundException("The native-network endpoint profile is not registered.");
            return new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints.Where(endpoint =>
                    endpoint.ExpectedEndpointId != expectedEndpointId),
                profile.CompactSerialEndpoints,
                profile.Kel103SerialEndpoints);
        }, cancellationToken);
    }

    public Task AddKel103Async(string profilePath, string backupPath,
        DesktopRuntimeHostKel103SerialEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile =>
            new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints,
                profile.CompactSerialEndpoints,
                profile.Kel103SerialEndpoints.Concat([endpoint])), cancellationToken);
    }

    public Task RemoveKel103Async(string profilePath, string backupPath,
        string expectedEndpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEndpointId);
        return EditAsync(profilePath, backupPath, profile =>
        {
            if (!profile.Kel103SerialEndpoints.Any(endpoint =>
                    endpoint.ExpectedEndpointId == expectedEndpointId))
                throw new KeyNotFoundException("The KEL-103 serial endpoint profile is not registered.");
            return new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints,
                profile.CompactSerialEndpoints,
                profile.Kel103SerialEndpoints.Where(endpoint =>
                    endpoint.ExpectedEndpointId != expectedEndpointId));
        }, cancellationToken);
    }

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

    private static async Task WriteAsync(string path,
        DesktopRuntimeHostEndpointCompositionProfile profile,
        CancellationToken cancellationToken)
    {
        IEnumerable<object> endpoints = profile.NativeNetworkEndpoints.Select(endpoint => (object)new
        {
            kind = "NativeNetwork", expectedEndpointId = endpoint.ExpectedEndpointId,
            host = endpoint.Host, port = endpoint.Port
        }).Concat(profile.CompactSerialEndpoints.Select(endpoint => (object)new
        {
            kind = "CompactSerial", expectedEndpointId = endpoint.ExpectedEndpointId,
            vendorId = endpoint.VendorId, productId = endpoint.ProductId,
            baudRate = endpoint.BaudRate,
            verificationTimeoutMilliseconds = (int)endpoint.VerificationTimeout.TotalMilliseconds
        })).Concat(profile.Kel103SerialEndpoints.Select(endpoint => (object)new
        {
            kind = "Kel103Serial", expectedEndpointId = endpoint.ExpectedEndpointId,
            definitionId = endpoint.DefinitionReference.Id.Value,
            definitionVersion = endpoint.DefinitionReference.Version,
            serialPort = endpoint.SerialPort, baudRate = endpoint.BaudRate
        }));
        await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream,
            new { formatVersion = 1, endpoints },
            new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

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
