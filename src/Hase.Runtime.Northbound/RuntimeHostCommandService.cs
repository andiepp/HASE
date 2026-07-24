namespace Hase.Runtime.Northbound;

/// <summary>
/// Composes normalized Command execution over one shared
/// attachment-generation projection.
/// </summary>
internal sealed class RuntimeHostCommandService
    : IRuntimeHostCommandService
{
    private readonly RuntimeHostCommandExecutor
        _commandExecutor;

    public RuntimeHostCommandService(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        ArgumentNullException.ThrowIfNull(
            attachmentProjection);

        _commandExecutor =
            new RuntimeHostCommandExecutor(
                attachmentProjection);
    }

    /// <inheritdoc />
    public Task<RuntimeHostCommandOperationResult> ExecuteAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        return _commandExecutor.ExecuteAsync(
            target,
            argument,
            cancellationToken);
    }
}