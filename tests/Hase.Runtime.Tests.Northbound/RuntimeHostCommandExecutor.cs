using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Resolves and executes generation-scoped Commands through attachment-bound
/// operation ports.
/// </summary>
internal sealed class RuntimeHostCommandExecutor
{
    private readonly RuntimeHostAttachmentProjection
        _attachmentProjection;

    public RuntimeHostCommandExecutor(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        _attachmentProjection =
            attachmentProjection
            ?? throw new ArgumentNullException(
                nameof(attachmentProjection));
    }

    public async Task<RuntimeHostCommandOperationResult> ExecuteAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        cancellationToken.ThrowIfCancellationRequested();

        RuntimeHostPublishedAttachment? attachment =
            _attachmentProjection.Find(
                target.EndpointId);

        if (attachment is null
            || attachment.Generation
                != target.AttachmentGeneration)
        {
            return RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.AttachmentNotCurrent);
        }

        RuntimeEndpoint runtimeEndpoint =
            attachment.Entry.RuntimeEndpoint;

        RuntimeInstrument? runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                target.InstrumentId);

        if (runtimeInstrument is null)
        {
            return RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.InstrumentNotFound);
        }

        RuntimeCommand? runtimeCommand =
            runtimeInstrument.FindCommand(
                target.CommandPath);

        if (runtimeCommand is null)
        {
            return RuntimeHostCommandOperationResult.Failed(
                RuntimeHostCommandOperationStatus.CommandNotFound);
        }

        IEndpointAttachmentCommandOperations commandOperations =
            attachment
                .Entry
                .AttachmentSession
                .CommandOperations;

        EndpointAttachmentCommandOperationResult operationResult =
            await commandOperations.ExecuteAsync(
                target.InstrumentId,
                target.CommandPath,
                argument,
                cancellationToken);

        return MapResult(
            operationResult);
    }

    private static RuntimeHostCommandOperationResult MapResult(
        EndpointAttachmentCommandOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.IsSuccess)
        {
            return RuntimeHostCommandOperationResult.Successful(
                result.ReturnValue);
        }

        RuntimeHostCommandOperationStatus status =
            result.Status switch
            {
                EndpointAttachmentCommandOperationStatus
                    .ArgumentNotSupported =>
                        RuntimeHostCommandOperationStatus
                            .ArgumentNotSupported,

                EndpointAttachmentCommandOperationStatus.Rejected =>
                    RuntimeHostCommandOperationStatus.EndpointRejected,

                EndpointAttachmentCommandOperationStatus.Unavailable =>
                    RuntimeHostCommandOperationStatus.EndpointUnavailable,

                EndpointAttachmentCommandOperationStatus.TimedOut =>
                    RuntimeHostCommandOperationStatus.TimedOut,

                EndpointAttachmentCommandOperationStatus.Failure
                    or EndpointAttachmentCommandOperationStatus.Success =>
                        RuntimeHostCommandOperationStatus.EndpointFailure,

                _ =>
                    RuntimeHostCommandOperationStatus.EndpointFailure
            };

        return RuntimeHostCommandOperationResult.Failed(
            status,
            result.Diagnostic);
    }
}