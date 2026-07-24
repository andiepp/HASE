namespace Hase.Runtime.Northbound;

/// <summary>
/// Composes cached queries and authoritative reads and writes over one shared
/// attachment-generation projection.
/// </summary>
internal sealed class RuntimeHostPropertyService
    : IRuntimeHostPropertyService
{
    private readonly RuntimeHostCachedPropertyResolver
        _cachedPropertyResolver;

    private readonly RuntimeHostPropertyReader
        _propertyReader;

    private readonly RuntimeHostPropertyWriter
        _propertyWriter;

    public RuntimeHostPropertyService(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        ArgumentNullException.ThrowIfNull(
            attachmentProjection);

        _cachedPropertyResolver =
            new RuntimeHostCachedPropertyResolver(
                attachmentProjection);

        _propertyReader =
            new RuntimeHostPropertyReader(
                attachmentProjection);

        _propertyWriter =
            new RuntimeHostPropertyWriter(
                attachmentProjection);
    }

    /// <inheritdoc />
    public RuntimeHostCachedPropertyResult GetCached(
        RuntimeHostPropertyTarget target)
    {
        return _cachedPropertyResolver.GetCached(
            target);
    }

    /// <inheritdoc />
    public Task<RuntimeHostPropertyOperationResult> ReadAsync(
        RuntimeHostPropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        return _propertyReader.ReadAsync(
            target,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RuntimeHostPropertyOperationResult> WriteAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        return _propertyWriter.WriteAsync(
            target,
            requestedValue,
            cancellationToken);
    }
}