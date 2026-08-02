using System.Text.Json;

namespace Hase.DesktopHost.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileEditor
{
    public Task AddNativeAsync(string profilePath, string backupPath,
        DesktopRuntimeHostNativeNetworkEndpointProfile endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return EditAsync(profilePath, backupPath, profile =>
            new DesktopRuntimeHostEndpointCompositionProfile(
                profile.NativeNetworkEndpoints.Concat([endpoint]),
                profile.CompactSerialEndpoints), cancellationToken);
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
                profile.CompactSerialEndpoints);
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
