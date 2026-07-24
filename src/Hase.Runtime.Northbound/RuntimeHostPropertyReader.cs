using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Resolves and executes generation-scoped authoritative Property reads
/// through attachment-bound operation ports.
/// </summary>
internal sealed class RuntimeHostPropertyReader
{
    private readonly RuntimeHostAttachmentProjection
        _attachmentProjection;

    public RuntimeHostPropertyReader(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        _attachmentProjection =
            attachmentProjection
            ?? throw new ArgumentNullException(
                nameof(attachmentProjection));
    }

    public async Task<RuntimeHostPropertyOperationResult> ReadAsync(
        RuntimeHostPropertyTarget target,
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
            return RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.AttachmentNotCurrent);
        }

        RuntimeEndpoint runtimeEndpoint =
            attachment.Entry.RuntimeEndpoint;

        RuntimeInstrument? runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                target.InstrumentId);

        if (runtimeInstrument is null)
        {
            return RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.InstrumentNotFound);
        }

        RuntimeProperty? runtimeProperty =
            runtimeInstrument.FindProperty(
                target.PropertyId);

        if (runtimeProperty is null)
        {
            return RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.PropertyNotFound);
        }

        if (!IsReadable(
                runtimeProperty.Descriptor))
        {
            return RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.ReadNotSupported);
        }

        IEndpointAttachmentPropertyOperations propertyOperations =
            attachment
                .Entry
                .AttachmentSession
                .PropertyOperations;

        EndpointAttachmentPropertyOperationResult operationResult =
            await propertyOperations.ReadAsync(
                target.InstrumentId,
                target.PropertyId,
                cancellationToken);

        return MapResult(
            operationResult);
    }

    private static bool IsReadable(
        PropertyDescriptor descriptor)
    {
        return (
            descriptor.AccessMode
            & PropertyAccessMode.Read)
            == PropertyAccessMode.Read;
    }

    private static RuntimeHostPropertyOperationResult MapResult(
        EndpointAttachmentPropertyOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.IsSuccess)
        {
            return RuntimeHostPropertyOperationResult.Successful(
                result.ConfirmedValue
                ?? throw new InvalidDataException(
                    "A successful attachment Property read did not contain "
                    + "a confirmed value."));
        }

        RuntimeHostPropertyOperationStatus status =
            result.Status switch
            {
                EndpointAttachmentPropertyOperationStatus.NotSupported =>
                    RuntimeHostPropertyOperationStatus.ReadNotSupported,

                EndpointAttachmentPropertyOperationStatus.Rejected =>
                    RuntimeHostPropertyOperationStatus.EndpointRejected,

                EndpointAttachmentPropertyOperationStatus.Unavailable =>
                    RuntimeHostPropertyOperationStatus.EndpointUnavailable,

                EndpointAttachmentPropertyOperationStatus.TimedOut =>
                    RuntimeHostPropertyOperationStatus.TimedOut,

                EndpointAttachmentPropertyOperationStatus.InvalidValue
                    or EndpointAttachmentPropertyOperationStatus.Failure
                    or EndpointAttachmentPropertyOperationStatus.Success =>
                        RuntimeHostPropertyOperationStatus.EndpointFailure,

                _ =>
                    RuntimeHostPropertyOperationStatus.EndpointFailure
            };

        return RuntimeHostPropertyOperationResult.Failed(
            status,
            result.Diagnostic);
    }
}