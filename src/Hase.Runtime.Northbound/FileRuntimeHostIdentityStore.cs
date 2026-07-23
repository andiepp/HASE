namespace Hase.Runtime.Northbound;

/// <summary>
/// Persists authoritative runtime-host identity in one versioned JSON file.
/// </summary>
public sealed class FileRuntimeHostIdentityStore
    : IRuntimeHostIdentityStore
{
    private readonly RuntimeHostIdentityFile
        _identityFile;

    /// <summary>
    /// Initializes a file-based runtime-host identity store.
    /// </summary>
    /// <param name="filePath">
    /// The fully qualified identity-document path selected by runtime-host
    /// composition.
    /// </param>
    public FileRuntimeHostIdentityStore(
        string filePath)
    {
        _identityFile =
            new RuntimeHostIdentityFile(
                filePath);
    }

    /// <inheritdoc />
    public Task<RuntimeHostId?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        return _identityFile.ReadAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RuntimeHostIdentityStoreCreateResult> CreateIfMissingAsync(
        RuntimeHostId candidate,
        CancellationToken cancellationToken = default)
    {
        return _identityFile.CreateIfMissingAsync(
            candidate,
            cancellationToken);
    }
}
