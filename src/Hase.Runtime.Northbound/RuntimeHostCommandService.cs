using Hase.Runtime.Diagnostics;

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

    private readonly RuntimeDiagnosticPublisher
        _diagnostics;

    public RuntimeHostCommandService(
        RuntimeHostAttachmentProjection attachmentProjection,
        RuntimeDiagnosticPublisher? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(
            attachmentProjection);

        _commandExecutor =
            new RuntimeHostCommandExecutor(
                attachmentProjection);

        _diagnostics =
            diagnostics
            ?? new RuntimeDiagnosticPublisher();
    }

    /// <inheritdoc />
    public async Task<RuntimeHostCommandOperationResult> ExecuteAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        RuntimeDiagnosticOperation operation =
            new(
                _diagnostics,
                RuntimeDiagnosticCategory.RuntimeCommand,
                "CommandExecutionStarted",
                "CommandExecutionCompleted",
                "CommandExecutionFailed",
                target.EndpointId.Value,
                target.AttachmentGeneration.Value,
                details:
                    new Dictionary<string, string>
                    {
                        ["instrument"] =
                            target.InstrumentId.Value,
                        ["path"] =
                            target.CommandPath.ToString()
                    });

        return await operation
            .RunAsync(
                token =>
                    _commandExecutor.ExecuteAsync(
                        target,
                        argument,
                        token),
                SelectOutcome,
                cancellationToken)
            .ConfigureAwait(
                false);
    }

    private static RuntimeDiagnosticOutcome SelectOutcome(
        RuntimeHostCommandOperationResult result)
    {
        return result.Status switch
        {
            RuntimeHostCommandOperationStatus.Success =>
                RuntimeDiagnosticOutcome.Succeeded,

            RuntimeHostCommandOperationStatus.TimedOut =>
                RuntimeDiagnosticOutcome.TimedOut,

            _ =>
                RuntimeDiagnosticOutcome.Failed
        };
    }
}
