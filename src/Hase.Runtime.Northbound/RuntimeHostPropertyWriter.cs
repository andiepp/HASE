using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Resolves and executes generation-scoped authoritative Property writes
/// through attachment-bound operation ports.
/// </summary>
internal sealed class RuntimeHostPropertyWriter
{
    private readonly RuntimeHostAttachmentProjection
        _attachmentProjection;

    public RuntimeHostPropertyWriter(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        _attachmentProjection =
            attachmentProjection
            ?? throw new ArgumentNullException(
                nameof(attachmentProjection));
    }

    public async Task<RuntimeHostPropertyOperationResult> WriteAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
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

        if (!IsWritable(
                runtimeProperty.Descriptor))
        {
            return RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.WriteNotSupported);
        }

        if (!RuntimeHostPropertyValueValidator.IsValid(
                runtimeProperty.Descriptor,
                requestedValue))
        {
            return RuntimeHostPropertyOperationResult.Failed(
                RuntimeHostPropertyOperationStatus.InvalidValue);
        }

        IEndpointAttachmentPropertyOperations propertyOperations =
            attachment
                .Entry
                .AttachmentSession
                .PropertyOperations;

        EndpointAttachmentPropertyOperationResult operationResult =
            await propertyOperations.WriteAsync(
                target.InstrumentId,
                target.PropertyId,
                requestedValue,
                cancellationToken);

        return MapResult(
            operationResult);
    }

    private static bool IsWritable(
        PropertyDescriptor descriptor)
    {
        return (
            descriptor.AccessMode
            & PropertyAccessMode.Write)
            == PropertyAccessMode.Write;
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
                    "A successful attachment Property write did not contain "
                    + "a confirmed value."));
        }

        RuntimeHostPropertyOperationStatus status =
            result.Status switch
            {
                EndpointAttachmentPropertyOperationStatus.NotSupported =>
                    RuntimeHostPropertyOperationStatus.WriteNotSupported,

                EndpointAttachmentPropertyOperationStatus.InvalidValue =>
                    RuntimeHostPropertyOperationStatus.InvalidValue,

                EndpointAttachmentPropertyOperationStatus.Rejected =>
                    RuntimeHostPropertyOperationStatus.EndpointRejected,

                EndpointAttachmentPropertyOperationStatus.Unavailable =>
                    RuntimeHostPropertyOperationStatus.EndpointUnavailable,

                EndpointAttachmentPropertyOperationStatus.TimedOut =>
                    RuntimeHostPropertyOperationStatus.TimedOut,

                EndpointAttachmentPropertyOperationStatus.Failure
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