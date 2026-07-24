using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Provides a safe unavailable Property port for non-production operational
/// resource implementations that do not own an endpoint coordinator.
/// </summary>
internal sealed class UnavailableEndpointAttachmentPropertyOperations
    : IEndpointAttachmentPropertyOperations
{
    private UnavailableEndpointAttachmentPropertyOperations()
    {
    }

    internal static UnavailableEndpointAttachmentPropertyOperations Instance
    {
        get;
    } =
        new();

    /// <inheritdoc />
    public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            propertyId);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            CreateUnavailableResult());
    }

    /// <inheritdoc />
    public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
        InstrumentId instrumentId,
        PropertyId propertyId,
        object? requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            instrumentId);

        ArgumentNullException.ThrowIfNull(
            propertyId);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            CreateUnavailableResult());
    }

    private static EndpointAttachmentPropertyOperationResult
        CreateUnavailableResult()
    {
        return EndpointAttachmentPropertyOperationResult.Failed(
            EndpointAttachmentPropertyOperationStatus.Unavailable,
            "The operational resource does not own an active Property port.");
    }
}