using Hase.Runtime.Diagnostics;

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

    private readonly RuntimeDiagnosticPublisher
        _diagnostics;

    public RuntimeHostPropertyService(
        RuntimeHostAttachmentProjection attachmentProjection,
        RuntimeDiagnosticPublisher? diagnostics = null)
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

        _diagnostics =
            diagnostics
            ?? new RuntimeDiagnosticPublisher();
    }

    /// <inheritdoc />
    public RuntimeHostCachedPropertyResult GetCached(
        RuntimeHostPropertyTarget target)
    {
        return _cachedPropertyResolver.GetCached(
            target);
    }

    /// <inheritdoc />
    public async Task<RuntimeHostPropertyOperationResult> ReadAsync(
        RuntimeHostPropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                target,
                "PropertyReadStarted",
                "PropertyReadCompleted",
                "PropertyReadFailed");

        return await operation
            .RunAsync(
                token =>
                    _propertyReader.ReadAsync(
                        target,
                        token),
                SelectOutcome,
                cancellationToken)
            .ConfigureAwait(
                false);
    }

    /// <inheritdoc />
    public async Task<RuntimeHostPropertyOperationResult> WriteAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        RuntimeDiagnosticOperation operation =
            CreateOperation(
                target,
                "PropertyWriteStarted",
                "PropertyWriteCompleted",
                "PropertyWriteFailed");

        return await operation
            .RunAsync(
                token =>
                    _propertyWriter.WriteAsync(
                        target,
                        requestedValue,
                        token),
                SelectOutcome,
                cancellationToken)
            .ConfigureAwait(
                false);
    }

    private RuntimeDiagnosticOperation CreateOperation(
        RuntimeHostPropertyTarget target,
        string startedEventName,
        string completedEventName,
        string failedEventName)
    {
        return new RuntimeDiagnosticOperation(
            _diagnostics,
            RuntimeDiagnosticCategory.RuntimeProperty,
            startedEventName,
            completedEventName,
            failedEventName,
            target.EndpointId.Value,
            target.AttachmentGeneration.Value,
            details:
                new Dictionary<string, string>
                {
                    ["instrument"] =
                        target.InstrumentId.Value,
                    ["path"] =
                        target.PropertyId.Value
                });
    }

    private static RuntimeDiagnosticOutcome SelectOutcome(
        RuntimeHostPropertyOperationResult result)
    {
        return result.Status switch
        {
            RuntimeHostPropertyOperationStatus.Success =>
                RuntimeDiagnosticOutcome.Succeeded,

            RuntimeHostPropertyOperationStatus.TimedOut =>
                RuntimeDiagnosticOutcome.TimedOut,

            _ =>
                RuntimeDiagnosticOutcome.Failed
        };
    }
}
