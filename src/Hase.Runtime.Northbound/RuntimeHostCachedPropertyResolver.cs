using Hase.Runtime.Runtime;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Resolves immutable cached Property snapshots from the current shared
/// attachment projection without communicating with endpoints.
/// </summary>
internal sealed class RuntimeHostCachedPropertyResolver
{
    private readonly RuntimeHostAttachmentProjection
        _attachmentProjection;

    public RuntimeHostCachedPropertyResolver(
        RuntimeHostAttachmentProjection attachmentProjection)
    {
        _attachmentProjection =
            attachmentProjection
            ?? throw new ArgumentNullException(
                nameof(attachmentProjection));
    }

    /// <summary>
    /// Resolves one generation-scoped cached Property snapshot.
    /// </summary>
    public RuntimeHostCachedPropertyResult GetCached(
        RuntimeHostPropertyTarget target)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        RuntimeHostPublishedAttachment? attachment =
            _attachmentProjection.Find(
                target.EndpointId);

        if (attachment is null
            || attachment.Generation
                != target.AttachmentGeneration)
        {
            return RuntimeHostCachedPropertyResult.Failed(
                RuntimeHostPropertyOperationStatus.AttachmentNotCurrent);
        }

        RuntimeEndpoint runtimeEndpoint =
            attachment.Entry.RuntimeEndpoint;

        RuntimeInstrument? runtimeInstrument =
            runtimeEndpoint.FindInstrument(
                target.InstrumentId);

        if (runtimeInstrument is null)
        {
            return RuntimeHostCachedPropertyResult.Failed(
                RuntimeHostPropertyOperationStatus.InstrumentNotFound);
        }

        RuntimeProperty? runtimeProperty =
            runtimeInstrument.FindProperty(
                target.PropertyId);

        if (runtimeProperty is null)
        {
            return RuntimeHostCachedPropertyResult.Failed(
                RuntimeHostPropertyOperationStatus.PropertyNotFound);
        }

        return RuntimeHostCachedPropertyResult.Successful(
            new PublishedRuntimePropertySnapshot(
                target,
                runtimeProperty.Descriptor,
                runtimeEndpoint.ConnectionStatus,
                runtimeProperty.CurrentValue));
    }
}