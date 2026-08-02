namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides read-only strict access to an existing Runtime Host identity file.
/// </summary>
public static class RuntimeHostIdentityReader
{
    public static Task<RuntimeHostId?> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        new RuntimeHostIdentityFile(filePath).ReadAsync(cancellationToken);
}
